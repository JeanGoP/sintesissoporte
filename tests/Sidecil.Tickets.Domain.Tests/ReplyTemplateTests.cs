using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sidecil.Tickets.Infrastructure;
using Xunit;

namespace Sidecil.Tickets.Domain.Tests;

public sealed partial class PortalOidcTests
{
    [Fact]
    public async Task OnlyAdminManagesTemplatesAndAgentsSeeOnlyEnabledOnes()
    {
        using var factory = new PortalOidcFactory();
        HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var admin = Client();
        using var agent = Client();
        using var requester = Client();
        var title = "Plantilla prueba " + Guid.NewGuid();
        Guid templateId = Guid.Empty;
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                async Task Login(HttpClient client, string role)
                {
                    var email = Guid.NewGuid() + "@test.invalid";
                    var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Prueba plantillas", Role = role,
                        OrganizationId = TicketsDbContext.GuestOrganizationId };
                    Assert.True((await users.CreateAsync(user, "Test-only!Password987")).Succeeded);
                    factory.CreatedUsers.Add(user.Id);
                    await Csrf(client);
                    (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Test-only!Password987" })).EnsureSuccessStatusCode();
                    await Csrf(client);
                }
                await Login(admin, "Admin");
                await Login(agent, "Agent");
                await Login(requester, "Requester");
            }
            var payload = new { title, body = "Hola {nombre}, revisamos el ticket {numero}.", enabled = true };
            Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync("/api/v1/admin/reply-templates", payload)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await requester.GetAsync("/api/v1/reply-templates")).StatusCode);
            Assert.Contains((await agent.GetFromJsonAsync<JsonElement>("/api/v1/reply-templates")).EnumerateArray(),
                x => x.GetProperty("title").GetString() == "Solicitar más información");
            var created = await admin.PostAsJsonAsync("/api/v1/admin/reply-templates", payload);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            templateId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/reply-templates", payload)).StatusCode);
            Assert.Contains((await agent.GetFromJsonAsync<JsonElement>("/api/v1/reply-templates")).EnumerateArray(),
                x => x.GetProperty("id").GetGuid() == templateId);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/v1/admin/reply-templates/" + templateId,
                new { title, body = "Respuesta mejorada para {asunto}.", enabled = false })).StatusCode);
            Assert.DoesNotContain((await agent.GetFromJsonAsync<JsonElement>("/api/v1/reply-templates")).EnumerateArray(),
                x => x.GetProperty("id").GetGuid() == templateId);
            Assert.Contains((await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/reply-templates")).EnumerateArray(),
                x => x.GetProperty("id").GetGuid() == templateId && !x.GetProperty("enabled").GetBoolean());
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/v1/admin/reply-templates/" + templateId)).StatusCode);
            templateId = Guid.Empty;
        }
        finally
        {
            if (templateId != Guid.Empty)
            {
                using var scope = factory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().ReplyTemplates
                    .Where(t => t.Id == templateId).ExecuteDeleteAsync();
            }
        }
    }
}
