using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed partial class PortalOidcTests
{
    [Fact]
    public async Task ChatPortalIdentityRequiresSessionCsrfAndConversationToken()
    {
        using var factory = new PortalOidcFactory();
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Mail:Mode", "Pickup"));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var site = new ChatSite { Name = "Portal prueba", Origin = "https://erp.example.invalid" };
        string email = Guid.NewGuid() + "@test.invalid";
        using (var scope = app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            db.ChatSites.Add(site); await db.SaveChangesAsync();
            var user = new ApplicationUser { UserName = email, Email = email, Role = "Requester", DisplayName = "Cuenta real", OrganizationId = TicketsDbContext.GuestOrganizationId };
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password)).Succeeded);
            factory.CreatedUsers.Add(user.Id);
        }
        try {
            client.DefaultRequestHeaders.Add("X-Sidecil-Widget", "1");
            var started = await client.PostAsJsonAsync("/api/v1/chat/sessions", new { siteId = site.Id }); started.EnsureSuccessStatusCode();
            var value = await started.Content.ReadFromJsonAsync<JsonElement>();
            var id = value.GetProperty("id").GetGuid(); var token = value.GetProperty("token").GetString();
            var path = "/api/v1/chat/sessions/" + id;
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            await Csrf(client);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/chat-session", new { id, token })).StatusCode);
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/tickets")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/chat-session", new { id, token })).StatusCode);
            await Csrf(client);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/chat-session", new { id, token = new string('0', 64) })).StatusCode);
            (await client.PostAsJsonAsync("/api/v1/auth/chat-session", new { id, token })).EnsureSuccessStatusCode();
            var snapshot = await client.GetFromJsonAsync<JsonElement>(path);
            Assert.True(snapshot.GetProperty("verified").GetBoolean());
            Assert.Equal(email, snapshot.GetProperty("email").GetString());
            Assert.Equal("Cuenta real", snapshot.GetProperty("name").GetString());
            (await client.GetAsync(path + "/tickets")).EnsureSuccessStatusCode();
            using var check = app.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<TicketsDbContext>();
            Assert.False(await db.OutboundEmails.AnyAsync(m => m.Recipient == email));
            await db.ChatConversations.Where(s => s.Id == id).ExecuteUpdateAsync(v => v.SetProperty(s => s.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/tickets")).StatusCode);
        } finally {
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.ChatConversations.Where(s => s.SiteId == site.Id).ExecuteDeleteAsync();
            await db.ChatSites.Where(s => s.Id == site.Id).ExecuteDeleteAsync();
        }
    }
}
