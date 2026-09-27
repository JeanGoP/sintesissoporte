using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

namespace Sidecil.Tickets.Api;
public record ChatCodeRequest([property: Required, RegularExpression(@"^\d{8}$")] string Code);
public record ChatSendRequest(Guid? TicketId);
public record ChatPortalRequest(Guid Id, [property: Required, StringLength(64, MinimumLength = 64)] string Token);

public static class ChatIdentity
{
    public static bool Verified(ChatConversation s) => s.IdentityVerifiedAt != null && s.ExpiresAt > DateTime.UtcNow &&
        (s.IdentityProvider == "Email" && s.IdentitySubject == s.Email || s.IdentityProvider == "Portal");
    public static IQueryable<Ticket> Owned(TicketsDbContext db, ChatConversation s) {
        if (!Verified(s)) return db.Tickets.Where(t => false);
        var normalized = s.Email.ToUpperInvariant();
        if (s.IdentityProvider == "Portal") return db.Tickets.Where(t => t.RequesterId == s.IdentitySubject ||
            t.RequesterId == null && t.GuestEmail == s.Email && db.Users.Any(u => u.Id == s.IdentitySubject && u.EmailConfirmed && u.NormalizedEmail == normalized));
        return db.Tickets.Where(t => t.RequesterId == null ? t.GuestEmail == s.Email :
            db.Users.Any(u => u.Id == t.RequesterId && u.NormalizedEmail == normalized));
    }
    public static IQueryable<Ticket> Pending(IQueryable<Ticket> query) => query.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
    public static void Map(RouteGroupBuilder sessions) {
        sessions.MapPost("/verification", async (HttpContext http, TicketsDbContext db, EmailComposer mail, IOptions<MailOptions> options) => {
            var s = (ChatConversation)http.Items["chat"]!;
            if (s.GuestSubmissionId != null) return Results.Conflict();
            if (Verified(s)) return Results.NoContent();
            if (options.Value.Mode == "Disabled") return Results.Problem("El correo no está disponible.", statusCode: 503);
            if (s.Name.Length < 2 || !new EmailAddressAttribute().IsValid(s.Email)) return Results.BadRequest();
            var now = DateTime.UtcNow;
            if (s.VerificationSentAt > now.AddMinutes(-1) || await db.OutboundEmails.CountAsync(x => x.Kind == "ChatCode" && x.Recipient == s.Email && x.CreatedAt > now.AddHours(-1)) >= 3)
                return Results.Problem("Espera antes de solicitar otro código. Revisa el correo y la carpeta de spam.", statusCode: 429);
            var code = RandomNumberGenerator.GetInt32(100000000).ToString("D8");
            s.VerificationHash = EmailComposer.Hash(s.TokenHash + ":" + s.Email + ":" + code);
            s.VerificationExpiresAt = now.AddMinutes(10); s.VerificationSentAt = now; s.VerificationAttempts = 0;
            mail.Queue(s.Email, "Código para tu chat de Sidecil", $"Hola {s.Name},\n\nTu código es: {code}\n\nEscríbelo únicamente en el chat que tú abriste. Vence en 10 minutos. No lo compartas. Si no lo solicitaste, ignora este correo.", "chat-code:" + Guid.NewGuid(), "ChatCode", expires: s.VerificationExpiresAt);
            await db.SaveChangesAsync(); return Results.NoContent();
        }).RequireRateLimiting("guest");
        sessions.MapPost("/verify", async (ChatCodeRequest request, HttpContext http, TicketsDbContext db) => {
            var s = (ChatConversation)http.Items["chat"]!;
            if (Verified(s)) return Results.NoContent();
            if (s.GuestSubmissionId != null || s.VerificationHash == null || s.VerificationExpiresAt <= DateTime.UtcNow || s.VerificationAttempts >= 5)
                return Results.Problem("El código venció o agotó sus intentos. Solicita uno nuevo.", statusCode: 400);
            s.VerificationAttempts++;
            var valid = CryptographicOperations.FixedTimeEquals(Convert.FromHexString(s.VerificationHash), Convert.FromHexString(EmailComposer.Hash(s.TokenHash + ":" + s.Email + ":" + request.Code)));
            if (valid) { s.IdentityProvider = "Email"; s.IdentityIssuer = "Sidecil"; s.IdentitySubject = s.Email; s.IdentityVerifiedAt = DateTime.UtcNow; s.VerificationHash = null; }
            await db.SaveChangesAsync();
            return valid ? Results.NoContent() : Results.Problem("El código no coincide. Revisa el último correo recibido.", statusCode: 400);
        }).RequireRateLimiting("login");
        sessions.MapGet("/tickets", async (HttpContext http, TicketsDbContext db) => {
            var s = (ChatConversation)http.Items["chat"]!;
            if (!Verified(s)) return Results.Unauthorized();
            var tickets = await Pending(Owned(db, s)).OrderByDescending(x => x.UpdatedAt)
                .Select(x => new { id = x.PublicId, number = EmailComposer.Number(x.Id), x.Subject, x.Status, x.UpdatedAt }).ToListAsync();
            return Results.Ok(tickets);
        });
        sessions.MapPost("/next", async (HttpContext http, TicketsDbContext db) => {
            var s = (ChatConversation)http.Items["chat"]!;
            if (!Verified(s)) return Results.Unauthorized();
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var next = new ChatConversation { SiteId = s.SiteId, TokenHash = EmailComposer.Hash(token), Name = s.Name, Email = s.Email,
                IdentityProvider = s.IdentityProvider, IdentityIssuer = s.IdentityIssuer, IdentitySubject = s.IdentitySubject, IdentityVerifiedAt = s.IdentityVerifiedAt, ExpiresAt = s.ExpiresAt };
            db.ChatConversations.Add(next); await db.SaveChangesAsync(); return Results.Ok(new { next.Id, token, next.ExpiresAt });
        }).RequireRateLimiting("guest");
        sessions.MapPost("/send", SendAsync);
    }
    public static async Task<IResult> PortalAsync(ChatPortalRequest request, HttpContext http, TicketsDbContext db, UserManager<ApplicationUser> users) {
        var user = await users.GetUserAsync(http.User);
        if (user == null) return Results.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync();
        var s = await db.ChatConversations.FromSqlInterpolated($"SELECT * FROM [chat].[Conversations] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {request.Id}").SingleOrDefaultAsync();
        if (s == null || s.ExpiresAt <= DateTime.UtcNow || s.GuestSubmissionId != null ||
            s.TokenHash != EmailComposer.Hash(request.Token) || !await db.ChatSites.AnyAsync(x => x.Id == s.SiteId && x.Enabled)) return Results.Unauthorized();
        if (Verified(s)) return Results.Conflict();
        s.Name = user.DisplayName; s.Email = user.Email!.ToLowerInvariant(); s.IdentityProvider = "Portal";
        s.IdentityIssuer = "Sidecil"; s.IdentitySubject = user.Id; s.IdentityVerifiedAt = DateTime.UtcNow; s.VerificationHash = null;
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.NoContent();
    }
    private static async Task<IResult> SendAsync(ChatSendRequest request, HttpContext http, TicketsDbContext db, EmailComposer mail) {
        var s = (ChatConversation)http.Items["chat"]!;
        if (!Verified(s)) return Results.Unauthorized();
        if (s.GuestSubmissionId is {} pendingId) {
            var existing = await db.GuestSubmissions.FindAsync(pendingId);
            return existing?.TicketId is {} ticketId ? Results.Ok(new { number = EmailComposer.Number(ticketId) }) : Results.Conflict();
        }
        if (s.Body.Trim().Length < 10) return Results.Problem("Describe tu solicitud con al menos 10 caracteres.", statusCode: 400);
        Ticket ticket;
        var normalizedEmail = s.Email.ToUpperInvariant();
        ApplicationUser? user = s.IdentityProvider == "Portal" ? await db.Users.FindAsync(s.IdentitySubject) : await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (request.TicketId is {} publicId) {
            ticket = (await db.Tickets.FromSqlInterpolated($"SELECT * FROM [tickets].[Tickets] WITH (UPDLOCK, ROWLOCK) WHERE [PublicId] = {publicId}").SingleOrDefaultAsync())!;
            if (ticket == null || !await Pending(Owned(db, s)).AnyAsync(t => t.Id == ticket.Id)) return Results.NotFound();
            ticket.UpdatedAt = DateTime.UtcNow; ticket.HasCustomerReply = true;
            if (ticket.Status == TicketStatus.WaitingRequester) ticket.Status = TicketStatus.InProgress;
        } else {
            if (s.Subject.Length < 5 || s.Module.Length < 2) return Results.Problem("Completa el módulo y el asunto.", statusCode: 400);
            var team = await db.Teams.OrderBy(t => t.Name).FirstOrDefaultAsync();
            if (team == null) return Results.Conflict();
            ticket = new Ticket { Subject = s.Subject, Category = s.Category, RequesterId = user?.Id, GuestName = user == null ? s.Name : null,
                GuestEmail = user == null ? s.Email : null, OrganizationId = user?.OrganizationId ?? TicketsDbContext.GuestOrganizationId,
                TeamId = team.Id, Priority = TicketPriority.Normal, DueAt = DateTime.UtcNow.AddHours(24) };
            db.Tickets.Add(ticket);
        }
        ticket.Messages.Add(new TicketMessage { AuthorId = user?.Id, AuthorName = s.Name, Body = request.TicketId == null ? $"Módulo: {s.Module}\n\n{s.Body}" : s.Body, Source = "Chat", Visibility = MessageVisibility.Public });
        ticket.Events.Add(new TicketEvent { ActorId = user?.Id, ActorName = s.Name, Kind = request.TicketId == null ? "Created" : "CustomerReply", Detail = "Solicitud recibida desde chat con identidad verificada." });
        await db.SaveChangesAsync();
        var pending = new GuestSubmission { Name = s.Name, Email = s.Email, TokenHash = EmailComposer.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))), TicketId = ticket.Id, ExpiresAt = s.ExpiresAt };
        db.GuestSubmissions.Add(pending); s.GuestSubmissionId = pending.Id;
        if (request.TicketId == null) await mail.ReceiptAsync(ticket);
        await db.SaveChangesAsync(); return Results.Ok(new { number = EmailComposer.Number(ticket.Id) });
    }
}
