using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

namespace Sidecil.Tickets.Api;

public static class PortalIdentity
{
    public static bool Enabled(IConfiguration config, string provider) => ExternalIdentityRules.Supported(provider) &&
        !string.IsNullOrWhiteSpace(config[$"ExternalLogin:{provider}:ClientId"]) &&
        !string.IsNullOrWhiteSpace(config[$"ExternalLogin:{provider}:ClientSecret"]);

    public static object Providers(IConfiguration config) => new[] { "Microsoft", "Google" }
        .Select(name => new { name, enabled = Enabled(config, name) }).ToArray();

    public static void Configure(WebApplicationBuilder builder)
    {
        foreach (var provider in new[] { "Microsoft", "Google" }) {
            if (!Enabled(builder.Configuration, provider)) continue;
            builder.Services.AddAuthentication().AddOpenIdConnect("Portal" + provider, options => {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Authority = provider == "Google" ? "https://accounts.google.com" : "https://login.microsoftonline.com/common/v2.0";
                options.ClientId = builder.Configuration[$"ExternalLogin:{provider}:ClientId"]!;
                options.ClientSecret = builder.Configuration[$"ExternalLogin:{provider}:ClientSecret"]!;
                options.CallbackPath = "/signin-" + provider.ToLowerInvariant();
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.Scope.Clear();
                options.Scope.Add("openid"); options.Scope.Add("profile"); options.Scope.Add("email");
                options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(5);
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.NonceCookie.SecurePolicy = options.CorrelationCookie.SecurePolicy;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.NameClaimType = "name";
                if (provider == "Microsoft") {
                    options.TokenValidationParameters.IssuerValidator = (issuer, token, _) => {
                        var tenant = token switch {
                            JsonWebToken jwt when jwt.TryGetPayloadValue<string>("tid", out var tid) => tid,
                            System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt => jwt.Claims.FirstOrDefault(x => x.Type == "tid")?.Value,
                            _ => null
                        };
                        return ExternalIdentityRules.ValidMicrosoftIssuer(issuer, tenant) ? issuer : throw new SecurityTokenInvalidIssuerException("Emisor de Microsoft inválido.");
                    };
                }
                options.Events = new OpenIdConnectEvents {
                    OnTokenValidated = context => {
                        if (context.Properties is not null) context.Properties.Items["portal-issuer"] = context.SecurityToken.Issuer;
                        return Task.CompletedTask;
                    },
                    // TicketReceived runs after signature, issuer, audience, nonce, correlation and code validation.
                    OnTicketReceived = async context => {
                        context.HandleResponse();
                        await CompleteAsync(context, provider);
                    },
                    OnRemoteFailure = context => { context.HandleResponse(); context.Response.Redirect(FrontendHosting.ReturnUrl(context.HttpContext, "/?external=failed")); return Task.CompletedTask; }
                };
            });
        }
    }

    private static async Task CompleteAsync(TicketReceivedContext context, string provider)
    {
        var http = context.HttpContext;
        void Failed() => http.Response.Redirect(FrontendHosting.ReturnUrl(http, "/?external=failed"));
        if (context.Properties is null || !context.Properties.Items.TryGetValue("portal-attempt", out var attemptId) || !Guid.TryParse(attemptId, out var id)) { Failed(); return; }
        var db = http.RequestServices.GetRequiredService<TicketsDbContext>();
        var users = http.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var signIn = http.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var attempt = await db.PortalLoginAttempts.SingleOrDefaultAsync(x => x.Id == id && x.Provider == provider);
        var subject = context.Principal?.FindFirstValue("sub");
        context.Properties.Items.TryGetValue("portal-issuer", out var issuer);
        if (attempt is null || attempt.StartedAt is null || attempt.CompletedAt != null || attempt.ExpiresAt < DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer)) { Failed(); return; }
        if (await db.PortalLoginAttempts.Where(x => x.Id == id && x.CompletedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompletedAt, DateTime.UtcNow)) != 1) { Failed(); return; }
        var key = EmailComposer.Hash(issuer + "\n" + subject);
        if (attempt.LinkUserId is { } userId) {
            var session = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            var user = session.Succeeded ? await users.GetUserAsync(session.Principal!) : null;
            if (user?.Id != userId || await users.GetSecurityStampAsync(user) != attempt.SecurityStamp || await users.IsLockedOutAsync(user)) { Failed(); return; }
            var owner = await users.FindByLoginAsync(provider, key);
            if (owner is not null || (await users.GetLoginsAsync(user)).Any(x => x.LoginProvider == provider)) { http.Response.Redirect(FrontendHosting.ReturnUrl(http, "/account?external=conflict")); return; }
            var result = await users.AddLoginAsync(user, new UserLoginInfo(provider, key, provider));
            if (!result.Succeeded) { Failed(); return; }
            await tx.CommitAsync();
            http.Response.Redirect(FrontendHosting.ReturnUrl(http, "/account?external=linked"));
        } else {
            var user = await users.FindByLoginAsync(provider, key);
            if (user is null) { await tx.CommitAsync(); http.Response.Redirect(FrontendHosting.ReturnUrl(http, "/?external=unlinked")); return; }
            // Identity checks lockout and any configured confirmation/2FA requirements.
            var result = await signIn.ExternalLoginSignInAsync(provider, key, isPersistent: false, bypassTwoFactor: false);
            await tx.CommitAsync();
            http.Response.Redirect(FrontendHosting.ReturnUrl(http, result.Succeeded ? "/" : "/?external=failed"));
        }
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/auth/providers", (IConfiguration config) => Results.Ok(Providers(config)));
        app.MapPost("/api/v1/auth/external/{provider}", (string provider, TicketsDbContext db, IConfiguration config) => BeginAsync(provider, null, db, config)).RequireRateLimiting("login");
        var account = app.MapGroup("/api/v1/auth/connections").RequireAuthorization().AddEndpointFilter<ValidationFilter>().RequireRateLimiting("login");
        account.MapGet("", async (HttpContext http, UserManager<ApplicationUser> users, IConfiguration config) => {
            var user = (await users.GetUserAsync(http.User))!;
            var linked = (await users.GetLoginsAsync(user)).Select(x => x.LoginProvider).ToArray();
            return Results.Ok(new { providers = Providers(config), linked });
        });
        account.MapPost("/{provider}", async (string provider, LinkExternalRequest request, HttpContext http, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, TicketsDbContext db, IConfiguration config) => {
            var user = (await users.GetUserAsync(http.User))!;
            if (!(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
                return Results.Problem("No fue posible validar tu contraseña actual.", statusCode: 400);
            if ((await users.GetLoginsAsync(user)).Any(x => x.LoginProvider == provider)) return Results.Conflict();
            return await BeginAsync(provider, user, db, config);
        });
        account.MapPost("/{provider}/remove", async (string provider, LinkExternalRequest request, HttpContext http, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) => {
            var user = (await users.GetUserAsync(http.User))!;
            if (!(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
                return Results.Problem("No fue posible validar tu contraseña actual.", statusCode: 400);
            var login = (await users.GetLoginsAsync(user)).SingleOrDefault(x => x.LoginProvider == provider);
            if (login is null) return Results.NotFound();
            if (!(await users.RemoveLoginAsync(user, provider, login.ProviderKey)).Succeeded) return Results.Conflict();
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        });
        app.MapGet("/auth/external/start", async (string? key, TicketsDbContext db, IConfiguration config) => {
            if (key is null || key.Length != 64) return Results.BadRequest();
            var hash = EmailComposer.Hash(key);
            var attempt = await db.PortalLoginAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.KeyHash == hash);
            if (attempt is null || !Enabled(config, attempt.Provider) || attempt.ExpiresAt < DateTime.UtcNow || attempt.StartedAt != null || attempt.CompletedAt != null) return Results.BadRequest();
            if (await db.PortalLoginAttempts.Where(x => x.Id == attempt.Id && x.StartedAt == null && x.CompletedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StartedAt, DateTime.UtcNow)) != 1) return Results.BadRequest();
            var properties = new AuthenticationProperties { RedirectUri = "/" };
            properties.Items["portal-attempt"] = attempt.Id.ToString();
            return Results.Challenge(properties, ["Portal" + attempt.Provider]);
        }).RequireRateLimiting("login");
    }

    private static async Task<IResult> BeginAsync(string provider, ApplicationUser? user, TicketsDbContext db, IConfiguration config)
    {
        if (!ExternalIdentityRules.Supported(provider)) return Results.NotFound();
        if (!Enabled(config, provider)) return Results.Problem("Este proveedor todavía no está configurado. Ingresa con tu correo y contraseña.", statusCode: 503);
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PortalLoginAttempts.Add(new PortalLoginAttempt { Provider = provider, KeyHash = EmailComposer.Hash(key), LinkUserId = user?.Id, SecurityStamp = user?.SecurityStamp });
        await db.SaveChangesAsync();
        return Results.Ok(new { url = "/auth/external/start?key=" + key });
    }
}
