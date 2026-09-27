using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifiedChatListsOnlyOwnPendingTicketsAndCanContinueOrCreate(bool registered)
    {
        using var factory = new PortalOidcFactory();
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Mail:Mode", "Pickup"));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Sidecil-Widget", "1");
        var email = Guid.NewGuid() + "@test.invalid";
        var site = new ChatSite { Name = "Prueba", Origin = "https://erp.example.invalid" };
        string? userId = null;
        Guid own, other, closed;
        using (var scope = app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            if (registered) {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Cliente", Role = "Requester", OrganizationId = TicketsDbContext.GuestOrganizationId };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded); userId = user.Id; factory.CreatedUsers.Add(userId);
            }
            var team = await db.Teams.Select(t => t.Id).FirstAsync();
            Ticket Add(string recipient, TicketStatus status, string? requester = null) {
                var t = new Ticket { Subject = "Problema de prueba", GuestEmail = recipient, GuestName = "Cliente", RequesterId = requester, OrganizationId = TicketsDbContext.GuestOrganizationId, TeamId = team, Status = status, DueAt = DateTime.UtcNow.AddDays(1) };
                t.Messages.Add(new TicketMessage { Body = "Nota interna secreta", Visibility = MessageVisibility.Internal });
                db.Tickets.Add(t); return t;
            }
            own = Add(email, TicketStatus.WaitingRequester, userId).PublicId;
            other = Add("other-" + email, TicketStatus.New).PublicId;
            closed = Add(email, TicketStatus.Closed, userId).PublicId;
            db.ChatSites.Add(site); await db.SaveChangesAsync();
        }
        try {
            var started = await client.PostAsJsonAsync("/api/v1/chat/sessions", new { siteId = site.Id });
            started.EnsureSuccessStatusCode();
            var session = await started.Content.ReadFromJsonAsync<JsonElement>();
            string path = "/api/v1/chat/sessions/" + session.GetProperty("id").GetString();
            client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("token").GetString());
            var draft = new { name = "Cliente", email, module = "Ventas", subject = "Problema de prueba", body = "Este es mi mensaje de seguimiento", category = "General" };
            (await client.PutAsJsonAsync(path + "/draft", draft)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/tickets")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path + "/send", new { ticketId = own })).StatusCode);
            (await client.PostAsJsonAsync(path + "/verification", new {})).EnsureSuccessStatusCode();
            string code;
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var mail = await db.OutboundEmails.SingleAsync(x => x.Recipient == email && x.Kind == "ChatCode");
                code = Regex.Match(mail.Body, @"\b\d{8}\b").Value;
                Assert.Equal(8, code.Length);
            }
            var wrong = code == "00000000" ? "11111111" : "00000000";
            for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/verify", new { code = wrong })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/verify", new { code })).StatusCode);
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.ChatConversations.Where(s => s.SiteId == site.Id).ExecuteUpdateAsync(v => v.SetProperty(s => s.VerificationSentAt, DateTime.UtcNow.AddMinutes(-2)));
            }
            var oldCode = code;
            (await client.PostAsJsonAsync(path + "/verification", new {})).EnsureSuccessStatusCode();
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                code = Regex.Match((await db.OutboundEmails.Where(x => x.Recipient == email && x.Kind == "ChatCode").OrderByDescending(x => x.CreatedAt).FirstAsync()).Body, @"\b\d{8}\b").Value;
                await db.ChatConversations.Where(s => s.SiteId == site.Id).ExecuteUpdateAsync(v => v.SetProperty(s => s.VerificationExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/verify", new { code })).StatusCode);
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                await db.ChatConversations.Where(s => s.SiteId == site.Id).ExecuteUpdateAsync(v => v.SetProperty(s => s.VerificationExpiresAt, DateTime.UtcNow.AddMinutes(10)));
            }
            (await client.PostAsJsonAsync(path + "/verify", new { code })).EnsureSuccessStatusCode();
            var list = await client.GetFromJsonAsync<JsonElement>(path + "/tickets");
            Assert.Equal(1, list.GetArrayLength());
            Assert.Equal(own, list[0].GetProperty("id").GetGuid());
            Assert.DoesNotContain("secreta", list.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path + "/draft", draft with { email = "other-" + email })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path + "/send", new { ticketId = other })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path + "/send", new { ticketId = closed })).StatusCode);
            using var upload = new HttpRequestMessage(HttpMethod.Post, path + "/attachments") { Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("Archivo de soporte de prueba")) };
            upload.Headers.Add("X-File-Name", Uri.EscapeDataString("captura de información.txt"));
            using var uploaded = await client.SendAsync(upload); uploaded.EnsureSuccessStatusCode();
            var attachmentId = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var response = await client.PostAsJsonAsync(path + "/send", new { ticketId = own }); response.EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync(path + "/send", new { ticketId = own })).EnsureSuccessStatusCode();
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                var t = await db.Tickets.Include(t => t.Messages).SingleAsync(t => t.PublicId == own);
                var attached = await (from file in db.ChatAttachments join conversation in db.ChatConversations on file.ConversationId equals conversation.Id join pending in db.GuestSubmissions on conversation.GuestSubmissionId equals pending.Id where file.Id == attachmentId && pending.TicketId == t.Id select file).SingleAsync();
                Assert.Equal("captura de información.txt", attached.FileName);
                Assert.Equal("Archivo de soporte de prueba", System.Text.Encoding.UTF8.GetString(attached.Content));
                Assert.True(t.HasCustomerReply); Assert.Equal(TicketStatus.InProgress, t.Status);
                Assert.Single(t.Messages, m => m.Source == "Chat");
                Assert.Equal(MessageVisibility.Public, t.Messages.Single(m => m.Source == "Chat").Visibility);
            }
            var next = await client.PostAsJsonAsync(path + "/next", new {}); next.EnsureSuccessStatusCode();
            session = await next.Content.ReadFromJsonAsync<JsonElement>();
            path = "/api/v1/chat/sessions/" + session.GetProperty("id").GetString();
            client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("token").GetString());
            (await client.PutAsJsonAsync(path + "/draft", draft)).EnsureSuccessStatusCode();
            (await client.GetAsync(path + "/tickets")).EnsureSuccessStatusCode();
            (await client.PostAsJsonAsync(path + "/send", new { ticketId = (Guid?)null })).EnsureSuccessStatusCode();
            using (var scope = app.Services.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
                Assert.Equal(2, await db.OutboundEmails.CountAsync(m => m.Recipient == email && m.Kind == "ChatCode"));
                var t = await db.Tickets.OrderByDescending(t => t.Id).FirstAsync(t => t.RequesterId == userId && (registered || t.GuestEmail == email));
                Assert.Equal(userId, t.RequesterId);
                Assert.Equal(1, await db.OutboundEmails.CountAsync(m => m.TicketId == t.Id && m.Kind == "Receipt"));
            }
        } finally {
            using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
            await db.ChatConversations.Where(s => s.SiteId == site.Id).ExecuteDeleteAsync();
            await db.ChatSites.Where(s => s.Id == site.Id).ExecuteDeleteAsync();
            await db.GuestSubmissions.Where(s => s.Email == email).ExecuteDeleteAsync();
            await db.OutboundEmails.Where(m => m.Recipient == email).ExecuteDeleteAsync();
            await db.Tickets.Where(t => t.GuestEmail == email || t.GuestEmail == "other-" + email || (userId != null && t.RequesterId == userId)).ExecuteDeleteAsync();
        }
    }
}
