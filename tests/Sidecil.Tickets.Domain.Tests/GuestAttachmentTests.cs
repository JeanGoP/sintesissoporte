using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sidecil.Tickets.Infrastructure;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed partial class PortalOidcTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GuestFilesWaitForConfirmationAndTransferOnlyOnce(bool withFiles)
    {
        using var factory = new PortalOidcFactory();
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Mail:Mode", "Pickup"));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var email = Guid.NewGuid() + "@test.invalid";
        var request = new { name = "Cliente invitado", email, subject = "Problema de inventario", body = "Descripción del problema de inventario", category = "General" };
        MultipartFormDataContent Form(string name) {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(request.name), "name"); form.Add(new StringContent(email), "email");
            form.Add(new StringContent(request.subject), "subject"); form.Add(new StringContent(request.body), "body"); form.Add(new StringContent(request.category), "category");
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Evidencia de prueba")), "files", name); return form;
        }
        try {
            await Csrf(client);
            if (withFiles) {
                using var invalid = Form("falso.png");
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/public/tickets/with-attachments", invalid)).StatusCode);
                using (var scope = app.Services.CreateScope()) Assert.False(await scope.ServiceProvider.GetRequiredService<TicketsDbContext>().GuestSubmissions.AnyAsync(x => x.Email == email));
                using var form = Form("evidencia.txt");
                Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/v1/public/tickets/with-attachments", form)).StatusCode);
            } else Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/public/tickets", request)).StatusCode);
            string token; Guid pendingId;
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var pending = await db.GuestSubmissions.SingleAsync(x => x.Email == email); pendingId = pending.Id;
                Assert.Null(pending.TicketId);
                Assert.False(await db.Tickets.AnyAsync(t => t.GuestEmail == email));
                Assert.Equal(withFiles ? 1 : 0, await db.GuestAttachments.CountAsync(f => f.GuestSubmissionId == pendingId));
                var message = await db.OutboundEmails.SingleAsync(m => m.Recipient == email && m.Kind == "Verification");
                token = message.Body.Split("#token=")[1].Split('\n')[0].Trim();
            }
            if (withFiles) {
                using (var scope = app.Services.CreateScope()) {
                    var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                    for (var i = 0; i < 2; i++) db.GuestSubmissions.Add(new Sidecil.Tickets.Domain.GuestSubmission { Email = email, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddDays(1) });
                    await db.SaveChangesAsync();
                }
                using var limited = Form("otro.txt");
                Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/v1/public/tickets/with-attachments", limited)).StatusCode);
            }
            for (var i = 0; i < 2; i++) (await client.PostAsJsonAsync("/api/v1/public/confirm", new { token })).EnsureSuccessStatusCode();
            Guid ticketId, fileId = Guid.Empty;
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var ticket = await db.Tickets.SingleAsync(t => t.GuestEmail == email); ticketId = ticket.PublicId;
                Assert.False(await db.GuestAttachments.AnyAsync(f => f.GuestSubmissionId == pendingId));
                var files = await db.TicketAttachments.Where(f => f.TicketId == ticket.Id).ToListAsync();
                Assert.Equal(withFiles ? 1 : 0, files.Count);
                if (withFiles) { fileId = files[0].Id; Assert.Equal("Evidencia de prueba", Encoding.UTF8.GetString(files[0].Content)); }                var expired = new Sidecil.Tickets.Domain.GuestSubmission { Email = email, Name = "Prueba", TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddDays(-1) };
                db.GuestSubmissions.Add(expired);
                db.GuestAttachments.Add(new Sidecil.Tickets.Domain.GuestAttachment { GuestSubmissionId = expired.Id, FileName = "vencido.txt", ContentType = "text/plain", Length = 1, Content = [65] });
                await db.SaveChangesAsync();
                var dispatcher = new Sidecil.Tickets.Infrastructure.Mail.OutboundDispatcher(db, Microsoft.Extensions.Options.Options.Create(new Sidecil.Tickets.Infrastructure.Mail.MailOptions()));
                await dispatcher.PurgeExpiredGuestAttachmentsAsync();
                Assert.False(await db.GuestAttachments.AnyAsync(f => f.GuestSubmissionId == expired.Id));
                Assert.Equal(withFiles ? 1 : 0, await db.TicketAttachments.CountAsync(f => f.TicketId == ticket.Id));
            }
            if (withFiles) {
                var url = $"/api/v1/tickets/{ticketId}/attachments/{fileId}";
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
                using var scope = app.Services.CreateScope();
                var admin = new ApplicationUser { UserName = Guid.NewGuid() + "@test.invalid", Email = Guid.NewGuid() + "@test.invalid", Role = "Admin", DisplayName = "Administrador", OrganizationId = TicketsDbContext.GuestOrganizationId };
                Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(admin, Password)).Succeeded); factory.CreatedUsers.Add(admin.Id);
                (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = Password })).EnsureSuccessStatusCode();
                using var download = await client.GetAsync(url); download.EnsureSuccessStatusCode();
                Assert.Equal("Evidencia de prueba", await download.Content.ReadAsStringAsync());
            }
        } finally {
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.GuestSubmissions.Where(x => x.Email == email).ExecuteDeleteAsync();
            await db.OutboundEmails.Where(x => x.Recipient == email).ExecuteDeleteAsync();
            await db.Tickets.Where(x => x.GuestEmail == email).ExecuteDeleteAsync();
        }
    }
}
