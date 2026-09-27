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
    public async Task ModulesProtectListsMessagesFilesAssignmentAndRevocation()
    {
        using var factory = new PortalOidcFactory();
        HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var admin = Client(); using var agent = Client(); using var requester = Client();
        string agentId = "", requesterId = ""; Guid module = Guid.Empty, category = Guid.Empty; var subject = "Prueba permisos " + Guid.NewGuid(); var company = new Organization { Name = "Empresa prueba " + Guid.NewGuid() };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>(); var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); db.Add(company); await db.SaveChangesAsync();
            async Task<string> Login(HttpClient c, string role) { var email = Guid.NewGuid() + "@test.invalid"; var u = new ApplicationUser { UserName = email, Email = email, DisplayName = "Prueba catálogo", Role = role, OrganizationId = TicketsDbContext.GuestOrganizationId, TeamId = await db.Teams.Select(t => (Guid?)t.Id).FirstAsync() }; Assert.True((await users.CreateAsync(u, Password)).Succeeded); factory.CreatedUsers.Add(u.Id); await Csrf(c); (await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).EnsureSuccessStatusCode(); await Csrf(c); return u.Id; }
            await Login(admin, "Admin"); agentId = await Login(agent, "Agent"); requesterId = await Login(requester, "Requester");
        }
        try
        {
            var name = "Categoría " + Guid.NewGuid().ToString("N");
            Assert.Equal(HttpStatusCode.Forbidden, (await requester.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { name })).StatusCode);
            var created = await admin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { name }); created.EnsureSuccessStatusCode(); category = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { name })).StatusCode);
            created = await admin.PostAsJsonAsync("/api/v1/admin/catalog/modules", new { name = "Inventario", categoryId = category }); created.EnsureSuccessStatusCode(); module = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var invalid = await requester.PostAsJsonAsync("/api/v1/tickets", new { subject, body = "Descripción de prueba extensa", category = "General", moduleId = module, companyName = "Empresa declarada", priority = "Normal" }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            invalid = await requester.PostAsJsonAsync("/api/v1/tickets", new { subject, body = "Descripción de prueba extensa", category = name, moduleId = module, organizationId = company.Id, priority = "Normal" }); Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);
            (await admin.PutAsJsonAsync("/api/v1/admin/catalog/users/" + requesterId + "/scope", new { moduleIds = Array.Empty<Guid>(), organizationIds = new[] { company.Id } })).EnsureSuccessStatusCode();
            created = await requester.PostAsJsonAsync("/api/v1/tickets", new { subject, body = "Descripción de prueba extensa", category = name, moduleId = module, organizationId = company.Id, priority = "Normal", assigneeId = agentId }); created.EnsureSuccessStatusCode(); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); var path = "/api/v1/tickets/" + id;
            Guid fileId;
            using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>(); var ticket = await db.Tickets.SingleAsync(t => t.PublicId == id); Assert.Null(ticket.AssigneeId); Assert.Equal(company.Id, ticket.OrganizationId); var file = new TicketAttachment { TicketId = ticket.Id, FileName = "prueba.txt", ContentType = "text/plain", Length = 1, Content = [65] }; fileId = file.Id; db.Add(file); await db.SaveChangesAsync(); }
            Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync(path + "/attachments/" + fileId)).StatusCode);
            var list = await agent.GetFromJsonAsync<JsonElement>("/api/v1/tickets?search=" + Uri.EscapeDataString(subject)); Assert.Equal(0, list.GetProperty("total").GetInt32());
            Assert.Equal(HttpStatusCode.NotFound, (await agent.PostAsJsonAsync(path + "/messages", new { body = "Mensaje no permitido", visibility = "Public" })).StatusCode);
            (await admin.PutAsJsonAsync("/api/v1/admin/catalog/users/" + agentId + "/scope", new { moduleIds = new[] { module }, organizationIds = Array.Empty<Guid>() })).EnsureSuccessStatusCode();
            var detail = await agent.GetFromJsonAsync<JsonElement>(path); Assert.Equal("Inventario", detail.GetProperty("module").GetString()); (await agent.GetAsync(path + "/attachments/" + fileId)).EnsureSuccessStatusCode();
            agent.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", detail.GetProperty("version").GetString());
            (await agent.PutAsJsonAsync(path + "/assignment", new { assigneeId = agentId })).EnsureSuccessStatusCode();
            detail = await agent.GetFromJsonAsync<JsonElement>(path); agent.DefaultRequestHeaders.Remove("If-Match"); agent.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", detail.GetProperty("version").GetString());
            (await agent.PostAsJsonAsync(path + "/messages", new { body = "Nota interna permitida", visibility = "Internal" })).EnsureSuccessStatusCode();
            (await admin.PutAsJsonAsync("/api/v1/admin/catalog/users/" + agentId + "/scope", new { moduleIds = Array.Empty<Guid>(), organizationIds = Array.Empty<Guid>() })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync(path)).StatusCode); detail = await admin.GetFromJsonAsync<JsonElement>(path); Assert.Equal(JsonValueKind.Null, detail.GetProperty("assigneeId").ValueKind);
            var customer = await requester.GetFromJsonAsync<JsonElement>(path); Assert.DoesNotContain(customer.GetProperty("messages").EnumerateArray(), m => m.GetProperty("body").GetString() == "Nota interna permitida");
            (await admin.PutAsJsonAsync("/api/v1/admin/catalog/modules/" + module, new { name = "Inventario", categoryId = category, enabled = false })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await requester.PostAsJsonAsync("/api/v1/tickets", new { subject, body = "Descripción de prueba extensa", category = name, moduleId = module, organizationId = company.Id, priority = "Normal" })).StatusCode);
            (await requester.GetAsync(path)).EnsureSuccessStatusCode();
            detail = await admin.GetFromJsonAsync<JsonElement>(path);
            admin.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", detail.GetProperty("version").GetString());
            var classification = new { moduleId = Guid.Parse("d1000000-0000-0000-0000-000000000002"), organizationId = company.Id };
            Assert.Equal(HttpStatusCode.Forbidden, (await requester.PutAsJsonAsync(path + "/classification", classification)).StatusCode);
            (await admin.PutAsJsonAsync(path + "/classification", classification)).EnsureSuccessStatusCode();
            var routed = await admin.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal("Soporte técnico", routed.GetProperty("category").GetString());
            var review = await admin.GetFromJsonAsync<JsonElement>("/api/v1/tickets?view=needs-routing&search=" + Uri.EscapeDataString(subject));
            Assert.Equal(1, review.GetProperty("total").GetInt32());
        }
        finally
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>(); var ids = db.Tickets.Where(t => t.Subject == subject).Select(t => t.Id); await db.OutboundEmails.Where(m => m.TicketId != null && ids.Contains(m.TicketId.Value)).ExecuteDeleteAsync(); await db.Tickets.Where(t => t.Subject == subject).ExecuteDeleteAsync(); await db.AgentModules.Where(x => x.ModuleId == module).ExecuteDeleteAsync(); await db.UserOrganizations.Where(x => x.OrganizationId == company.Id).ExecuteDeleteAsync(); await db.SupportModules.Where(x => x.Id == module).ExecuteDeleteAsync(); await db.SupportCategories.Where(x => x.Id == category).ExecuteDeleteAsync(); await db.Organizations.Where(x => x.Id == company.Id).ExecuteDeleteAsync();
        }
    }
}
