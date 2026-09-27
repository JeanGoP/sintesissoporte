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
    [Theory]
    [InlineData("Google")]
    [InlineData("Microsoft")]
    public async Task DeactivationRevokesCookiesAndExternalAccessAndPreservesHistory(string provider)
    {
        using var factory = new PortalOidcFactory();
        HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var admin = Client(); using var agent = Client(); using var stale = Client(); using var external = Client();
        string agentId = "", agentEmail = ""; Guid ticketId = Guid.Empty; string unusedId = ""; string adminId = "";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            async Task<ApplicationUser> Create(string role) { var email = Guid.NewGuid() + "@test.invalid"; var u = new ApplicationUser { UserName = email, Email = email, Role = role, DisplayName = "Prueba baja", OrganizationId = TicketsDbContext.GuestOrganizationId }; Assert.True((await users.CreateAsync(u, Password)).Succeeded); factory.CreatedUsers.Add(u.Id); return u; }
            var a = await Create("Admin"); adminId = a.Id; var b = await Create("Agent"); agentId = b.Id; agentEmail = b.Email!; unusedId = (await Create("Agent")).Id;
            async Task Login(HttpClient c, string email) { await Csrf(c); (await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).EnsureSuccessStatusCode(); await Csrf(c); }
            await Login(admin, a.Email!); await Login(agent, agentEmail); await Login(stale, agentEmail);
            var ticket = new Ticket { Subject = "Prueba baja", OrganizationId = TicketsDbContext.GuestOrganizationId, TeamId = await db.Teams.Select(t => t.Id).FirstAsync(), AssigneeId = agentId, ModuleId = Guid.Parse("d1000000-0000-0000-0000-000000000001"), DueAt = DateTime.UtcNow.AddDays(1) }; db.Add(ticket); await db.SaveChangesAsync(); ticketId = ticket.PublicId;
        }
        try
        {
            var callback = await Challenge(agent, factory, provider, true, "", false); Assert.Equal(HttpStatusCode.Redirect, (await agent.GetAsync(callback)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await agent.PutAsJsonAsync("/api/v1/admin/catalog/users/" + agentId + "/enabled", new { enabled = false })).StatusCode);
            (await admin.PutAsJsonAsync("/api/v1/admin/catalog/users/" + agentId + "/enabled", new { enabled = false })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/v1/auth/me")).StatusCode);
            await Csrf(agent); Assert.Equal(HttpStatusCode.Unauthorized, (await agent.PostAsJsonAsync("/api/v1/auth/login", new { email = agentEmail, password = Password })).StatusCode);
            callback = await Challenge(external, factory, provider, false, "", false); var denied = await external.GetAsync(callback); Assert.Contains("external=failed", denied.Headers.Location!.OriginalString); Assert.Equal(HttpStatusCode.Unauthorized, (await external.GetAsync("/api/v1/auth/me")).StatusCode);
            using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>(); Assert.Null((await db.Tickets.SingleAsync(t => t.PublicId == ticketId)).AssigneeId); Assert.True(await db.Users.AnyAsync(u => u.Id == agentId)); }
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync("/api/v1/admin/catalog/users/" + agentId)).StatusCode);
            (await admin.PutAsJsonAsync("/api/v1/admin/catalog/users/" + agentId + "/enabled", new { enabled = true })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await stale.GetAsync("/api/v1/auth/me")).StatusCode);
            await Csrf(agent); (await agent.PostAsJsonAsync("/api/v1/auth/login", new { email = agentEmail, password = Password })).EnsureSuccessStatusCode();
            (await admin.DeleteAsync("/api/v1/admin/catalog/users/" + unusedId)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync("/api/v1/admin/catalog/users/" + adminId)).StatusCode);
        }
        finally { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Tickets.Where(t => t.PublicId == ticketId).ExecuteDeleteAsync(); }
    }
    [Fact]
    public async Task CatalogDeletionExplainsDependenciesAndDeletesOnlyUnusedRecords()
    {
        using var factory = new PortalOidcFactory(); using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var category = new SupportCategory { Name = "Baja " + Guid.NewGuid() }; var module = new SupportModule { Name = "Módulo", CategoryId = category.Id }; var ticket = new Ticket { Subject = "Historial de baja", Category = category.Name, ModuleId = module.Id, OrganizationId = TicketsDbContext.GuestOrganizationId };
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var email = Guid.NewGuid() + "@test.invalid"; var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Administrador prueba", Role = "Admin", OrganizationId = TicketsDbContext.GuestOrganizationId }; Assert.True((await users.CreateAsync(user, Password)).Succeeded); factory.CreatedUsers.Add(user.Id); ticket.TeamId = await db.Teams.Select(t => t.Id).FirstAsync(); db.Add(category); db.Add(module); db.Add(ticket); await db.SaveChangesAsync(); await Csrf(admin); (await admin.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).EnsureSuccessStatusCode(); await Csrf(admin); }
        try
        {
            var response = await admin.DeleteAsync("/api/v1/admin/catalog/categories/" + category.Id); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Contains("módulos", await response.Content.ReadAsStringAsync());
            response = await admin.DeleteAsync("/api/v1/admin/catalog/modules/" + module.Id); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Contains("tickets", await response.Content.ReadAsStringAsync());
            using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Tickets.Where(t => t.Id == ticket.Id).ExecuteDeleteAsync();
            (await admin.DeleteAsync("/api/v1/admin/catalog/modules/" + module.Id)).EnsureSuccessStatusCode(); (await admin.DeleteAsync("/api/v1/admin/catalog/categories/" + category.Id)).EnsureSuccessStatusCode();
        }
        finally { using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>(); await db.Tickets.Where(t => t.Id == ticket.Id).ExecuteDeleteAsync(); await db.SupportModules.Where(m => m.Id == module.Id).ExecuteDeleteAsync(); await db.SupportCategories.Where(c => c.Id == category.Id).ExecuteDeleteAsync(); }
    }
}
