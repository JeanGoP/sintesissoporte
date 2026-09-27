using System.Net;
using System.Net.Http.Json;
using System.Text;
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
    public async Task PortalFilesAreValidatedSavedAtomicallyAndRestrictedToTicketOwner()
    {
        using var factory = new PortalOidcFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var other = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        string userId;
        using (var scope = factory.Services.CreateScope()) {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            async Task<string> Login(HttpClient c) {
                var email = Guid.NewGuid() + "@test.invalid";
                var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Persona prueba", Role = "Requester", OrganizationId = TicketsDbContext.GuestOrganizationId };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded); factory.CreatedUsers.Add(user.Id);
                await Csrf(c); (await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password })).EnsureSuccessStatusCode(); await Csrf(c); return user.Id;
            }
            userId = await Login(client); await Login(other);
        }
        MultipartFormDataContent Form(string name, byte[] bytes, int count = 1) {
            var f = new MultipartFormDataContent();
            f.Add(new StringContent("Problema con inventario"), "subject");
            f.Add(new StringContent("Descripción suficiente del problema"), "body");
            f.Add(new StringContent("General"), "category"); f.Add(new StringContent("Normal"), "priority");
            for (int i = 0; i < count; i++) f.Add(new ByteArrayContent(bytes), "files", name);
            return f;
        }
        try {
            using (var invalid = Form("imagen.png", Encoding.UTF8.GetBytes("Esto no es una imagen"))) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/tickets/with-attachments", invalid)).StatusCode);
            using (var excess = Form("archivo.txt", [65], 4)) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/tickets/with-attachments", excess)).StatusCode);
            using (var big = Form("archivo.txt", new byte[5 * 1024 * 1024 + 1])) Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/v1/tickets/with-attachments", big)).StatusCode);
            using (var scope = factory.Services.CreateScope()) Assert.False(await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().Tickets.AnyAsync(t => t.RequesterId == userId));
            using var valid = Form("evidencia.txt", Encoding.UTF8.GetBytes("Detalle del problema"));
            using var created = await client.PostAsync("/api/v1/tickets/with-attachments", valid); created.EnsureSuccessStatusCode();
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var detail = await client.GetFromJsonAsync<JsonElement>("/api/v1/tickets/" + id);
            var files = detail.GetProperty("attachments"); Assert.Equal(1, files.GetArrayLength());
            var fileId = files[0].GetProperty("id").GetGuid();
            Assert.Equal("evidencia.txt", files[0].GetProperty("fileName").GetString());
            var url = $"/api/v1/tickets/{id}/attachments/{fileId}";
            using var download = await client.GetAsync(url); download.EnsureSuccessStatusCode();
            Assert.Equal("Detalle del problema", await download.Content.ReadAsStringAsync());
            Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        } finally {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            var ids = db.Tickets.Where(t => t.RequesterId == userId).Select(t => t.Id);
            await db.OutboundEmails.Where(m => m.TicketId != null && ids.Contains(m.TicketId.Value)).ExecuteDeleteAsync();
            await db.Tickets.Where(t => t.RequesterId == userId).ExecuteDeleteAsync();
        }
    }
}
