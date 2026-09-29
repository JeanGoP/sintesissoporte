using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task AgentReceivesOnlyModuleEventsAndKeepsOwnSoundPreference()
    {
        using var factory = new PortalOidcFactory();
        HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var agent = Client();
        using var requester = Client();
        var prefix = "Aviso prueba " + Guid.NewGuid();
        string agentId = "";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            async Task<string> Login(HttpClient client, string role)
            {
                var email = Guid.NewGuid() + "@test.invalid";
                var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Prueba avisos", Role = role,
                    OrganizationId = TicketsDbContext.GuestOrganizationId };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                factory.CreatedUsers.Add(user.Id);
                await Csrf(client);
                (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).EnsureSuccessStatusCode();
                await Csrf(client);
                return user.Id;
            }
            agentId = await Login(agent, "Agent");
            await Login(requester, "Requester");
            db.AgentModules.Add(new AgentModule { UserId = agentId, ModuleId = Guid.Parse("d1000000-0000-0000-0000-000000000001") });
            await db.SaveChangesAsync();
        }
        try
        {
            var baseline = await agent.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
            Assert.Empty(baseline.GetProperty("items").EnumerateArray());
            var cursor = baseline.GetProperty("cursor").GetInt64();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var team = await db.Teams.Select(t => t.Id).FirstAsync();
                Ticket NewTicket(string subject, string module) => new() { Subject = subject,
                    ModuleId = Guid.Parse(module), OrganizationId = TicketsDbContext.GuestOrganizationId,
                    TeamId = team, GuestEmail = Guid.NewGuid() + "@test.invalid", GuestName = "Cliente",
                    DueAt = DateTime.UtcNow.AddHours(24) };
                var allowed = NewTicket(prefix + " permitido", "d1000000-0000-0000-0000-000000000001");
                var other = NewTicket(prefix + " otro módulo", "d1000000-0000-0000-0000-000000000002");
                db.Tickets.AddRange(allowed, other);
                await db.SaveChangesAsync();
                allowed.Events.Add(new TicketEvent { Kind = "Created", Detail = "Nuevo ticket" });
                allowed.Events.Add(new TicketEvent { Kind = "CustomerReply", Detail = "Nueva respuesta" });
                allowed.Events.Add(new TicketEvent { Kind = "EmailReply", Detail = "Respuesta por correo" });
                other.Events.Add(new TicketEvent { Kind = "Created", Detail = "Nuevo ticket" });
                await db.SaveChangesAsync();
            }
            var response = await agent.GetFromJsonAsync<JsonElement>("/api/v1/notifications?after=" + cursor);
            var items = response.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(3, items.Count);
            Assert.Equal(new[] { "ticket", "reply", "reply" }, items.Select(x => x.GetProperty("kind").GetString()));
            Assert.All(items, x => Assert.Contains("permitido", x.GetProperty("subject").GetString()));
            Assert.Equal(HttpStatusCode.Forbidden, (await requester.GetAsync("/api/v1/notifications")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await agent.PutAsJsonAsync("/api/v1/auth/notification-sound", new { sound = "Other" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await agent.PutAsJsonAsync("/api/v1/auth/notification-sound", new { sound = "Bell" })).StatusCode);
            var me = await agent.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
            Assert.Equal("Bell", me.GetProperty("notificationSound").GetString());
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.Tickets.Where(t => t.Subject.StartsWith(prefix)).ExecuteDeleteAsync();
            await db.AgentModules.Where(m => m.UserId == agentId).ExecuteDeleteAsync();
        }
    }
}
