using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

namespace Sidecil.Tickets.Api;

public static class ChatEndpoints
{
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/chat").RequireAuthorization().AddEndpointFilter<ValidationFilter>();
        admin.AddEndpointFilter(async (context, next) => {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            return (await users.GetUserAsync(context.HttpContext.User))?.Role == "Admin" ? await next(context) : Results.Forbid();
        });
        admin.MapGet("/sites", async (TicketsDbContext db) => Results.Ok(await db.ChatSites.OrderBy(x => x.Name).ToListAsync()));
        admin.MapPost("/sites", async (ChatSiteRequest request, TicketsDbContext db, IWebHostEnvironment env) => {
            var origin = ChatRules.NormalizeOrigin(request.Origin.Trim(), env.IsDevelopment());
            if (origin is null) return Results.Problem("Indica solo el origen HTTPS del sistema, por ejemplo https://erp.empresa.com, sin rutas. HTTP local solo se permite en desarrollo.", statusCode: 400);
            if (await db.ChatSites.CountAsync() >= 100) return Results.Problem("Se alcanzó el límite de 100 integraciones.", statusCode: 409);
            var site = new ChatSite { Name = request.Name.Trim(), Origin = origin };
            db.ChatSites.Add(site); await db.SaveChangesAsync();
            return Results.Created("/api/v1/admin/chat/sites/" + site.Id, site);
        });
        admin.MapPut("/sites/{id:guid}", async (Guid id, ChatSiteEnabledRequest request, TicketsDbContext db) => {
            var site = await db.ChatSites.FindAsync(id);
            if (site is null) return Results.NotFound();
            site.Enabled = request.Enabled; await db.SaveChangesAsync(); return Results.NoContent();
        });

        // This surface NEVER uses login cookies. Each private conversation requires its own bearer token.
        var chat = app.MapGroup("/api/v1/chat").AddEndpointFilter<ValidationFilter>().RequireRateLimiting("chat");
        chat.AddEndpointFilter(async (context, next) => {
            if (context.HttpContext.Request.Headers["X-Sidecil-Widget"] != "1") return Results.BadRequest();
            return await next(context);
        });
        chat.MapGet("/sites/{id:guid}", async (Guid id, TicketsDbContext db, IOptions<MailOptions> mail) => {
            var site = await db.ChatSites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Enabled);
            return site is null ? Results.NotFound() : Results.Ok(new { site.Name, available = mail.Value.Mode != "Disabled", testMode = mail.Value.Mode == "Pickup" });
        });
        chat.MapPost("/sessions", async (ChatStartRequest request, TicketsDbContext db, IOptions<MailOptions> mail) => {
            if (mail.Value.Mode == "Disabled") return Results.Problem("El canal de soporte todavía no está habilitado.", statusCode: 503);
            if (!await db.ChatSites.AnyAsync(x => x.Id == request.SiteId && x.Enabled)) return Results.NotFound();
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var session = new ChatConversation { SiteId = request.SiteId, TokenHash = EmailComposer.Hash(token) };
            db.ChatConversations.Add(session); await db.SaveChangesAsync();
            return Results.Ok(new { session.Id, token, session.ExpiresAt });
        }).RequireRateLimiting("guest");

        var sessions = chat.MapGroup("/sessions/{id:guid}");
        sessions.AddEndpointFilter(async (context, next) => {
            var http = context.HttpContext;
            var db = http.RequestServices.GetRequiredService<TicketsDbContext>();
            var id = Guid.Parse(http.Request.RouteValues["id"]!.ToString()!);
            var bearer = http.Request.Headers.Authorization.ToString();
            if (!bearer.StartsWith("Bearer ", StringComparison.Ordinal) || bearer.Length != 71) return Results.Unauthorized();
            var write = !HttpMethods.IsGet(http.Request.Method);
            await using var tx = write ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted) : null;
            var session = write
                ? await db.ChatConversations.FromSqlInterpolated($"SELECT * FROM [chat].[Conversations] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync()
                : await db.ChatConversations.SingleOrDefaultAsync(x => x.Id == id);
            if (session is null || session.ExpiresAt < DateTime.UtcNow ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(session.TokenHash), Encoding.ASCII.GetBytes(EmailComposer.Hash(bearer[7..]))) ||
                !await db.ChatSites.AnyAsync(x => x.Id == session.SiteId && x.Enabled)) return Results.Unauthorized();
            http.Items["chat"] = session;
            var result = await next(context);
            if (tx is not null) await tx.CommitAsync();
            return result;
        });
        sessions.MapGet("", async (HttpContext http, TicketsDbContext db) => {
            var session = Session(http);
            var ticketId = session.GuestSubmissionId is { } pendingId
                ? (await db.GuestSubmissions.FindAsync(pendingId))?.TicketId : null;
            var attachments = await db.ChatAttachments.Where(x => x.ConversationId == session.Id)
                .Select(x => new { x.Id, x.FileName, x.Length }).ToListAsync();
            return Results.Ok(new { session.Name, session.Email, session.Module, session.Subject, session.Body, session.Category,
                submitted = session.GuestSubmissionId != null, number = ticketId is { } ticket ? EmailComposer.Number(ticket) : null, attachments });
        });
        sessions.MapPut("/draft", async (ChatDraftRequest request, HttpContext http, TicketsDbContext db) => {
            var session = Session(http);
            if (session.GuestSubmissionId != null) return Locked();
            if (!ChatRules.Categories.Contains(request.Category)) return Results.BadRequest();
            session.Name = request.Name.Trim(); session.Email = request.Email.Trim().ToLowerInvariant();
            session.Module = request.Module.Trim(); session.Subject = request.Subject.Trim(); session.Body = request.Body.Trim(); session.Category = request.Category;
            await db.SaveChangesAsync(); return Results.NoContent();
        });
        sessions.MapPost("/attachments", async (HttpContext http, TicketsDbContext db) => {
            var session = Session(http);
            if (session.GuestSubmissionId != null) return Locked();
            if (await db.ChatAttachments.CountAsync(x => x.ConversationId == session.Id) >= ChatRules.MaxFiles)
                return Results.Problem("Puedes adjuntar hasta 3 archivos.", statusCode: 400);
            if (http.Request.ContentLength is not { } length || length is <= 0 or > ChatRules.MaxFileBytes)
                return Results.Problem("Cada archivo debe pesar entre 1 byte y 5 MB.", statusCode: 413);
            string name;
            try { name = ChatRules.SafeName(Uri.UnescapeDataString(http.Request.Headers["X-File-Name"].ToString())); }
            catch (UriFormatException) { return Results.BadRequest(); }
            if (string.IsNullOrWhiteSpace(name) || name.Length > 180) return Results.BadRequest();
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await http.Request.Body.ReadAsync(buffer, http.RequestAborted)) > 0) {
                if (memory.Length + read > ChatRules.MaxFileBytes) return Results.StatusCode(413);
                await memory.WriteAsync(buffer.AsMemory(0, read), http.RequestAborted);
            }
            var content = memory.ToArray();
            var type = ChatRules.FileType(name, content);
            if (type is null) return Results.Problem("Adjunta un PNG, JPG, PDF o TXT válido de hasta 5 MB.", statusCode: 400);
            var file = new ChatAttachment { ConversationId = session.Id, FileName = name, ContentType = type, Content = content, Length = content.Length };
            db.ChatAttachments.Add(file); await db.SaveChangesAsync();
            return Results.Ok(new { file.Id, file.FileName, file.Length });
        });
        sessions.MapDelete("/attachments/{fileId:guid}", async (Guid fileId, HttpContext http, TicketsDbContext db) => {
            var session = Session(http);
            if (session.GuestSubmissionId != null) return Locked();
            var file = await db.ChatAttachments.FirstOrDefaultAsync(x => x.Id == fileId && x.ConversationId == session.Id);
            if (file is null) return Results.NotFound();
            db.ChatAttachments.Remove(file); await db.SaveChangesAsync(); return Results.NoContent();
        });
        sessions.MapPost("/submit", async (HttpContext http, TicketsDbContext db, EmailComposer mail, IOptions<MailOptions> options) => {
            var session = Session(http);
            if (session.GuestSubmissionId != null) return Results.Accepted(value: new { message = "La solicitud ya fue enviada. Revisa tu correo para confirmarla." });
            if (options.Value.Mode == "Disabled") return Results.Problem("El canal de correo no está disponible.", statusCode: 503);
            if (session.Name.Length < 2 || !new EmailAddressAttribute().IsValid(session.Email) || session.Subject.Length < 5 || session.Body.Length < 10 || session.Module.Length < 2)
                return Results.Problem("Completa tu nombre, correo, módulo, asunto y descripción antes de enviar.", statusCode: 400);
            var site = await db.ChatSites.FindAsync(session.SiteId);
            var now = DateTime.UtcNow;
            if (await db.GuestSubmissions.CountAsync(x => x.Email == session.Email && x.CreatedAt > now.AddHours(-1)) >= 3)
                return Results.Problem("Ya recibimos varias solicitudes para este correo. Confirma el enlace recibido o inténtalo en una hora.", statusCode: 429);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var transcript = $"Solicitud recibida desde el chat de {site!.Name} ({site.Origin}).\n\nAsistente: ¿Cómo te llamas?\nCliente: {session.Name}\n\nAsistente: ¿Cuál es tu correo?\nCliente: {session.Email}\n\nAsistente: ¿En qué módulo necesitas ayuda?\nCliente: {session.Module}\n\nAsistente: Resume el problema.\nCliente: {session.Subject}\n\nAsistente: Cuéntanos qué ocurrió y qué esperabas que pasara.\nCliente: {session.Body}\n\nCategoría: {session.Category}\nEl cliente revisó y confirmó el resumen antes de enviarlo.";
            var pending = new GuestSubmission { TokenHash = EmailComposer.Hash(token), Name = session.Name, Email = session.Email,
                Subject = session.Subject, Category = session.Category, Body = transcript, ExpiresAt = session.ExpiresAt };
            db.GuestSubmissions.Add(pending); session.GuestSubmissionId = pending.Id;
            var link = options.Value.PublicBaseUrl.TrimEnd('/') + "/confirmar-solicitud#token=" + token;
            mail.Queue(session.Email, "Confirma tu solicitud a Sidecil", "Hola " + session.Name + ",\n\nRecibimos tu solicitud desde el chat de " + site.Name + ".\n\nPara verificar tu correo y crear el ticket con la conversación y sus archivos, abre este enlace y pulsa Confirmar:\n" + link + "\n\nNo necesitas crear una cuenta. El enlace caduca en 24 horas desde el inicio del chat. Si no realizaste esta solicitud, ignora el mensaje.", "verify:" + pending.Id, "Verification", expires: pending.ExpiresAt);
            await db.SaveChangesAsync();
            return Results.Accepted(value: new { message = "Revisa tu correo y confirma la solicitud para crear el ticket." });
        });
    }
    private static ChatConversation Session(HttpContext http) => (ChatConversation)http.Items["chat"]!;
    private static IResult Locked() => Results.Problem("Esta solicitud ya fue enviada. Puedes continuar por correo cuando confirmes el ticket.", statusCode: 409);
}
