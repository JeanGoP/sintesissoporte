using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Infrastructure;
using Xunit;

namespace Sidecil.Tickets.Domain.Tests;

public sealed partial class PortalOidcTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("Agent")]
    [InlineData("Requester")]
    public async Task InvitationsPreserveRoleRequireTokenAndCanOnlyBeUsedOnce(string role)
    {
        using var factory = new PortalOidcFactory();
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Mail:Mode", "Pickup").UseSetting("Frontend:PublicBaseUrl", "https://portal.example.invalid"));
        using var admin = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var guest = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        string email = Guid.NewGuid() + "@test.invalid";
        Guid team;
        using (var scope = app.Services.CreateScope()) {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var actor = new ApplicationUser { UserName = Guid.NewGuid() + "@test.invalid", Email = Guid.NewGuid() + "@test.invalid", DisplayName = "Administrador prueba", Role = "Admin", OrganizationId = TicketsDbContext.GuestOrganizationId };
            Assert.True((await users.CreateAsync(actor, Password)).Succeeded);
            factory.CreatedUsers.Add(actor.Id);
            await Csrf(admin);
            (await admin.PostAsJsonAsync("/api/v1/auth/login", new { email = actor.Email, password = Password })).EnsureSuccessStatusCode();
            team = await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Teams.Select(x => x.Id).FirstAsync();
        }
        await Csrf(admin);
        await Csrf(guest);
        var request = new { displayName = "Persona invitada", email, role, organizationId = TicketsDbContext.GuestOrganizationId, teamId = role == "Agent" ? (Guid?)team : null, moduleIds = new[] { Guid.Parse("d1000000-0000-0000-0000-000000000001") } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/admin/users", request)).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/v1/admin/users", request);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        factory.CreatedUsers.Add(id);
        async Task<string> Token() {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var mail = await db.OutboundEmails.Where(x => x.Recipient == email).OrderByDescending(x => x.CreatedAt).FirstAsync();
            Assert.Equal("Invitation", mail.Kind);
            Assert.Null(mail.TicketId);
            Assert.NotNull(mail.ExpiresAt);
            var link = mail.Body.Split('\n').Single(x => x.StartsWith("https://portal.example.invalid/activar-cuenta#"));
            return QueryHelpers.ParseQuery(new Uri(link).Fragment[1..])["token"].ToString();
        }
        var oldToken = await Token();
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/invitation", new { userId = id, token = "invalid", password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync($"/api/v1/admin/users/{id}/invitation", new {})).StatusCode);
        (await admin.PostAsJsonAsync($"/api/v1/admin/users/{id}/invitation", new {})).EnsureSuccessStatusCode();
        var token = await Token();
        Assert.NotEqual(oldToken, token);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/invitation", new { userId = id, token = oldToken, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/invitation", new { userId = id, token, password = "abcdefghijkl" })).StatusCode);
        // La caducidad es comprobada por el proveedor de tokens, no solo por la cola.
        var options = app.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
        options.TokenLifespan = TimeSpan.FromSeconds(-1);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/invitation", new { userId = id, token, password = Password })).StatusCode);
        options.TokenLifespan = TimeSpan.FromHours(24);
        (await guest.PostAsJsonAsync("/api/v1/auth/invitation", new { userId = id, token, password = Password })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/invitation", new { userId = id, token, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/admin/users/{id}/invitation", new {})).StatusCode);
        (await guest.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).EnsureSuccessStatusCode();
        var me = await guest.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal(role, me.GetProperty("role").GetString());
        Assert.True(me.GetProperty("hasPassword").GetBoolean());
        using var final = app.Services.CreateScope();
        var saved = await final.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id);
        Assert.True(saved!.EmailConfirmed);
        Assert.Equal(role == "Agent" ? (Guid?)team : null, saved.TeamId);
    }
}
