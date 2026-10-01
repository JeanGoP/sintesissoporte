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
    public async Task AssignedTicketsStayPrivateAndUnreadStateAndDateFiltersWork()
    {
        using var factory = new PortalOidcFactory();
        HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var owner = Client();
        using var other = Client();
        using var admin = Client();
        var prefix = "Bandeja prueba " + Guid.NewGuid();
        string ownerId = "";
        Guid assignedId = Guid.Empty;
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                async Task<string> Login(HttpClient client, string role)
                {
                    var email = Guid.NewGuid() + "@test.invalid";
                    var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Agente prueba", Role = role,
                        OrganizationId = TicketsDbContext.GuestOrganizationId };
                    Assert.True((await users.CreateAsync(user, "Test-only!Password987")).Succeeded);
                    factory.CreatedUsers.Add(user.Id);
                    await Csrf(client);
                    (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Test-only!Password987" })).EnsureSuccessStatusCode();
                    await Csrf(client);
                    return user.Id;
                }
                ownerId = await Login(owner, "Agent");
                var otherId = await Login(other, "Agent");
                await Login(admin, "Admin");
                var module = Guid.Parse("d1000000-0000-0000-0000-000000000001");
                db.AgentModules.AddRange(new AgentModule { UserId = ownerId, ModuleId = module }, new AgentModule { UserId = otherId, ModuleId = module });
                var team = await db.Teams.Select(t => t.Id).FirstAsync();
                var assigned = new Ticket { Subject = prefix + " asignado", ModuleId = module, AssigneeId = ownerId,
                    OrganizationId = TicketsDbContext.GuestOrganizationId, TeamId = team, DueAt = DateTime.UtcNow.AddDays(1) };
                var unassigned = new Ticket { Subject = prefix + " libre", ModuleId = module,
                    OrganizationId = TicketsDbContext.GuestOrganizationId, TeamId = team, CreatedAt = DateTime.UtcNow.AddDays(-5), DueAt = DateTime.UtcNow.AddDays(1) };
                assignedId = assigned.PublicId;
                db.Tickets.AddRange(assigned, unassigned);
                await db.SaveChangesAsync();
                assigned.Events.Add(new TicketEvent { Kind = "Created", Detail = "Nuevo" });
                unassigned.Events.Add(new TicketEvent { Kind = "Created", Detail = "Nuevo" });
                await db.SaveChangesAsync();
            }

            async Task<JsonElement> List(HttpClient client, string suffix = "") =>
                await client.GetFromJsonAsync<JsonElement>("/api/v1/tickets?search=" + Uri.EscapeDataString(prefix) + suffix);
            var ownerRows = (await List(owner)).GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, ownerRows.Count);
            Assert.True(ownerRows.Single(t => t.GetProperty("id").GetGuid() == assignedId).GetProperty("hasUnread").GetBoolean());
            Assert.Single((await List(other)).GetProperty("items").EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/tickets/" + assignedId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync("/api/v1/tickets/" + assignedId + "/seen", null)).StatusCode);
            Assert.Single((await List(admin, "&view=assigned")).GetProperty("items").EnumerateArray());

            var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
            Assert.Single((await List(owner, "&from=" + from)).GetProperty("items").EnumerateArray());
            var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/v1/tickets?from=" + from + "&to=" + to)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync("/api/v1/tickets/" + assignedId + "/seen", null)).StatusCode);
            Assert.False((await List(owner)).GetProperty("items").EnumerateArray()
                .Single(t => t.GetProperty("id").GetGuid() == assignedId).GetProperty("hasUnread").GetBoolean());
            var otherCursor = (await other.GetFromJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("cursor").GetInt64();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var id = await db.Tickets.Where(t => t.PublicId == assignedId).Select(t => t.Id).SingleAsync();
                db.Events.Add(new TicketEvent { TicketId = id, Kind = "EmailReply", Detail = "Respuesta" });
                await db.SaveChangesAsync();
            }
            Assert.True((await List(owner)).GetProperty("items").EnumerateArray()
                .Single(t => t.GetProperty("id").GetGuid() == assignedId).GetProperty("hasUnread").GetBoolean());
            Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/v1/notifications?after=" + otherCursor))
                .GetProperty("items").EnumerateArray());
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Tickets
                .Where(t => t.Subject.StartsWith(prefix)).ExecuteDeleteAsync();
        }
    }
}
