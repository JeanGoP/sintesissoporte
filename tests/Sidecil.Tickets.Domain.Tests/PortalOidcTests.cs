using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Xunit;

namespace Sidecil.Tickets.Domain.Tests;

public sealed class PortalOidcTests
{
    private const string Password = "Test-only!Password987";
    private static async Task Csrf(HttpClient client) {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    }
    private static async Task<string> Challenge(HttpClient client, PortalOidcFactory factory, string provider, bool linking, string failure) {
        await Csrf(client);
        var begin = await client.PostAsJsonAsync(linking ? "/api/v1/auth/connections/" + provider : "/api/v1/auth/external/" + provider, new { password = Password });
        begin.EnsureSuccessStatusCode();
        var link = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString();
        using var challenge = await client.GetAsync(link);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(new[] { "email", "openid", "profile" }, query["scope"].ToString().Split(' ').OrderBy(x => x));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(link)).StatusCode);
        factory.Backchannel.Provider = provider; factory.Backchannel.Failure = failure;
        factory.Backchannel.Nonce = query["nonce"].ToString(); factory.Backchannel.Challenge = query["code_challenge"].ToString();
        return QueryHelpers.AddQueryString("/signin-" + provider.ToLowerInvariant(), new Dictionary<string,string?> { ["state"] = query["state"].ToString(), ["code"] = "test-code" });
    }
    [Theory]
    [InlineData("Google", "")]
    [InlineData("Microsoft", "")]
    [InlineData("Google", "signedout")]
    [InlineData("Google", "nonce")]
    [InlineData("Google", "audience")]
    [InlineData("Google", "signature")]
    [InlineData("Microsoft", "issuer")]
    public async Task LinkingAndPortalLoginValidateIdentityAndPreserveRole(string provider, string failure) {
        using var factory = new PortalOidcFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        string id, email;
        using (var scope = factory.Services.CreateScope()) {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            email = Guid.NewGuid() + "@test.invalid";
            var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Persona portal", Role = "Requester", OrganizationId = TicketsDbContext.GuestOrganizationId };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            id = user.Id; factory.CreatedUsers.Add(id);
        }
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).StatusCode);
        var callback = await Challenge(client, factory, provider, true, failure);
        if (failure == "signedout") { await Csrf(client); await client.PostAsJsonAsync("/api/v1/auth/logout", new { }); }
        using var result = await client.GetAsync(callback);
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        if (failure.Length > 0) {
            Assert.Equal("/?external=failed", result.Headers.Location!.OriginalString);
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Empty(await users.GetLoginsAsync((await users.FindByIdAsync(id))!)); return;
        }
        Assert.True(result.Headers.Location!.OriginalString == "/account?external=linked", factory.Backchannel.LastFailure ?? "Vinculación rechazada");
        await Csrf(client); await client.PostAsJsonAsync("/api/v1/auth/logout", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        callback = await Challenge(client, factory, provider, false, "");
        using var login = await client.GetAsync(callback);
        Assert.Equal("/", login.Headers.Location!.OriginalString);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal(id, me.GetProperty("id").GetString()); Assert.Equal("Requester", me.GetProperty("role").GetString());
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/chat/sites")).StatusCode);
        using var replay = await client.GetAsync(callback);
        Assert.Equal("/?external=failed", replay.Headers.Location!.OriginalString);
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/connections/" + provider + "/remove", new { password = Password })).StatusCode);
        var connections = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/connections");
        Assert.Equal(0, connections.GetProperty("linked").GetArrayLength());
    }
    [Fact]
    public async Task UnlinkedEmailCannotTakeOverAnExistingAccount() {
        using var factory = new PortalOidcFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope()) {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "oidc-test@example.com", Email = "oidc-test@example.com", DisplayName = "No vinculado", Role = "Admin", OrganizationId = TicketsDbContext.GuestOrganizationId };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded); factory.CreatedUsers.Add(user.Id);
        }
        var callback = await Challenge(client, factory, "Google", false, "");
        var result = await client.GetAsync(callback);
        Assert.Equal("/?external=unlinked", result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}

public sealed class PortalOidcFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string,string?> previous = new();
    private bool cleaned;
    public List<string> CreatedUsers { get; } = [];
    public FakeIdentityBackchannel Backchannel { get; } = new();
    public PortalOidcFactory()
    {
        foreach (var provider in new[] { "Google", "Microsoft" }) foreach (var field in new[] { "ClientId", "ClientSecret" }) {
            var key = $"ExternalLogin__{provider}__{field}"; previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, field == "ClientId" ? "local-test-client" : "local-test-secret");
        }
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sidecil.Tickets.Api")));
        builder.ConfigureServices(services => {
            foreach (var provider in new[] { "Google", "Microsoft" }) services.PostConfigure<OpenIdConnectOptions>("Portal" + provider, options => {
                var issuer = provider == "Google" ? "https://accounts.google.com" : FakeIdentityBackchannel.MicrosoftIssuer;
                var configuration = new OpenIdConnectConfiguration { Issuer = issuer, AuthorizationEndpoint = issuer + "/authorize", TokenEndpoint = issuer + "/token" };
                configuration.SigningKeys.Add(Backchannel.Key);
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.Backchannel = new HttpClient(Backchannel, false);
                var original = options.Events.OnRemoteFailure;
                options.Events.OnRemoteFailure = async context => { Backchannel.LastFailure = context.Failure?.ToString(); await original(context); };
            });
        });
    }
    protected override void Dispose(bool disposing)
    {
        if (cleaned) return;
        cleaned = true;
        if (disposing && CreatedUsers.Count > 0) {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            db.Users.Where(x => CreatedUsers.Contains(x.Id)).ExecuteDelete();
        }
        base.Dispose(disposing);
        foreach (var value in previous) Environment.SetEnvironmentVariable(value.Key, value.Value);
        if (disposing) Backchannel.Dispose();
    }
}

public sealed class FakeIdentityBackchannel : HttpMessageHandler
{
    public const string Tenant = "9188040d-6c67-4c5b-b112-36a304b66dad";
    public const string MicrosoftIssuer = "https://login.microsoftonline.com/" + Tenant + "/v2.0";
    public RsaSecurityKey Key { get; } = new(RSA.Create(2048)) { KeyId = "test-signing-key" };
    public string Provider { get; set; } = "Google";
    public string Nonce { get; set; } = "";
    public string Challenge { get; set; } = "";
    public string? LastFailure { get; set; }
    public string Failure { get; set; } = "";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.RequestUri!.AbsolutePath.EndsWith("/token")) throw new InvalidOperationException("Unexpected backchannel request.");
        var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
        var actualChallenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString())));
        Assert.Equal(Challenge, actualChallenge);
        var claims = new List<Claim> { new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64), new("sub", "unique-provider-subject"), new("name", "Persona OIDC"), new("email", "oidc-test@example.com"), new("nonce", Failure == "nonce" ? "wrong-nonce" : Nonce) };
        if (Provider == "Microsoft") claims.Add(new Claim("tid", Tenant));
        var issuer = Failure == "issuer" ? "https://attacker.example" : Provider == "Google" ? "https://accounts.google.com" : MicrosoftIssuer;
        var key = Failure == "signature" ? new RsaSecurityKey(RSA.Create(2048)) { KeyId = Key.KeyId } : Key;
        var token = new JwtSecurityToken(issuer, Failure == "audience" ? "wrong-client" : "local-test-client", claims, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "local-test-access-token", token_type = "Bearer", expires_in = 300, id_token = new JwtSecurityTokenHandler().WriteToken(token) }) };
    }
}
