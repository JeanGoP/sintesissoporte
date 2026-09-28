using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

namespace Sidecil.Tickets.Api;

public record PublicAccessStart([property: Required, StringLength(120, MinimumLength = 2)] string Name,
    [property: Required, EmailAddress, StringLength(200)] string Email);
public record PublicAccessToken([property: Required, StringLength(64, MinimumLength = 64)] string Token);

public static class PublicGuestAccessEndpoints
{
    private static async Task<PublicGuestAccess?> Session(TicketsDbContext db, string token)
    {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return null;
        var hash = EmailComposer.Hash(token);
        return await db.PublicGuestAccesses.FirstOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresAt > DateTime.UtcNow);
    }
    private static IQueryable<Ticket> Owned(TicketsDbContext db, PublicGuestAccess session)
    {
        var email = session.Email;
        var normalized = email.ToUpperInvariant();
        return ChatIdentity.Pending(db.Tickets.Where(t => t.RequesterId == null
            ? t.GuestEmail == email
            : db.Users.Any(u => u.Id == t.RequesterId && u.NormalizedEmail == normalized)));
    }
    private static IResult Bad(string detail, int status = 400) => Results.Problem(detail, statusCode: status);
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/public/access/start", async (PublicAccessStart request, TicketsDbContext db, EmailComposer mail, IOptions<MailOptions> options) =>
        {
            if (options.Value.Mode == "Disabled") return Bad("El correo no está disponible.", 503);
            var email = request.Email.Trim().ToLowerInvariant();
            var name = request.Name.Trim();
            if (await db.PublicGuestAccesses.CountAsync(x => x.Email == email && x.CreatedAt > DateTime.UtcNow.AddHours(-1)) >= 3)
                return Results.Accepted(value: new { message = "Si recibiste un enlace reciente, revísalo en tu correo." });
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var session = new PublicGuestAccess { TokenHash = EmailComposer.Hash(token), Email = email, Name = name, ExpiresAt = DateTime.UtcNow.AddHours(1) };
            db.PublicGuestAccesses.Add(session);
            var link = options.Value.PublicBaseUrl.TrimEnd('/') + "/solicitar#access=" + token;
            mail.Queue(email, "Consulta tus tickets en Sidecil", "Hola " + name + ",\n\nAbre este enlace para consultar tus tickets pendientes o crear una solicitud nueva:\n" + link +
                "\n\nVence en una hora. No lo compartas. Si no lo solicitaste, ignora este correo.", "public-access:" + session.Id, "PublicAccess", expires: session.ExpiresAt);
            await db.SaveChangesAsync();
            return Results.Accepted(value: new { message = "Te enviamos un enlace para verificar tu correo. Revisa también la carpeta de spam." });
        }).AddEndpointFilter<ValidationFilter>().RequireRateLimiting("guest");

        app.MapPost("/api/v1/public/access/tickets", async (PublicAccessToken request, TicketsDbContext db) =>
        {
            var session = await Session(db, request.Token);
            if (session is null) return Results.Unauthorized();
            var tickets = await Owned(db, session).OrderByDescending(x => x.UpdatedAt).Take(50)
                .Select(x => new { id = x.PublicId, number = EmailComposer.Number(x.Id), x.Subject, x.Status, x.UpdatedAt }).ToListAsync();
            return Results.Ok(new { name = session.Name, email = session.Email, tickets });
        }).AddEndpointFilter<ValidationFilter>().RequireRateLimiting("guest");

        app.MapPost("/api/v1/public/access/send", async (HttpContext http, TicketsDbContext db, EmailComposer mail) =>
        {
            if (!http.Request.HasFormContentType || http.Request.ContentLength > 16 * 1024 * 1024)
                return Bad("Envía el formulario y hasta tres archivos de 5 MB.", 413);
            var limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 16 * 1024 * 1024;
            IFormCollection form;
            try { form = await http.Request.ReadFormAsync(new FormOptions { MemoryBufferThreshold = ChatRules.MaxFileBytes, MultipartBodyLengthLimit = 16 * 1024 * 1024, ValueLengthLimit = 16000 }, http.RequestAborted); }
            catch (InvalidDataException) { return Bad("No se pudieron leer los archivos.", 413); }
            var token = form["token"].ToString();
            var session = await Session(db, token);
            if (session is null) return Results.Unauthorized();
            var body = form["body"].ToString().Trim();
            if (body.Length is < 10 or > 12000) return Bad("Describe la solicitud con entre 10 y 12.000 caracteres.");
            if (form.Files.Count > ChatRules.MaxFiles) return Bad("Puedes adjuntar hasta tres archivos.");
            var attachments = new List<TicketAttachment>();
            foreach (var file in form.Files) {
                if (file.Length is <= 0 or > ChatRules.MaxFileBytes) return Bad("Cada archivo debe pesar entre 1 byte y 5 MB.");
                var name = ChatRules.SafeName(file.FileName);
                if (name.Length is 0 or > 180) return Bad("El nombre del archivo no es válido.");
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, http.RequestAborted);
                var bytes = stream.ToArray();
                var type = ChatRules.FileType(name, bytes);
                if (type is null) return Bad("Adjunta PNG, JPG, PDF o TXT válido.");
                attachments.Add(new TicketAttachment { FileName = name, ContentType = type, Length = bytes.Length, Content = bytes });
            }
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, http.RequestAborted);
            var locked = await db.PublicGuestAccesses.FromSqlInterpolated($"SELECT * FROM [communications].[PublicGuestAccesses] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {session.Id}").SingleAsync(http.RequestAborted);
            if (locked.ExpiresAt <= DateTime.UtcNow) return Results.Unauthorized();
            Ticket ticket;
            if (Guid.TryParse(form["ticketId"], out var publicId)) {
                ticket = (await db.Tickets.FromSqlInterpolated($"SELECT * FROM [tickets].[Tickets] WITH (UPDLOCK, ROWLOCK) WHERE [PublicId] = {publicId}").SingleOrDefaultAsync(http.RequestAborted))!;
                if (ticket is null || !await Owned(db, session).AnyAsync(x => x.Id == ticket.Id, http.RequestAborted)) return Results.NotFound();
                ticket.UpdatedAt = DateTime.UtcNow; ticket.HasCustomerReply = true;
                if (ticket.Status == TicketStatus.WaitingRequester) ticket.Status = TicketStatus.InProgress;
            } else if (!string.IsNullOrEmpty(form["ticketId"])) return Bad("Selecciona un ticket válido.");
            else {
                var subject = form["subject"].ToString().Trim();
                var category = form["category"].ToString();
                var moduleId = Guid.TryParse(form["moduleId"], out var parsed) ? parsed : (Guid?)null;
                var company = form["companyName"].ToString().Trim();
                if (subject.Length is < 5 or > 180 || await SupportCatalog.Resolve(db, moduleId, category) is null || !SupportCatalog.ValidCompany(company))
                    return Bad("Completa empresa, categoría, módulo y asunto.");
                var team = await db.Teams.OrderBy(x => x.Name).FirstOrDefaultAsync(http.RequestAborted);
                if (team is null) return Bad("No hay equipo de atención disponible.", 409);
                var requester = await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == session.Email.ToUpperInvariant(), http.RequestAborted);
                ticket = new Ticket { Subject = subject, Category = category, ModuleId = moduleId, CompanyName = company,
                    RequesterId = requester?.Id, GuestName = requester is null ? session.Name : null, GuestEmail = requester is null ? session.Email : null,
                    OrganizationId = requester?.OrganizationId ?? TicketsDbContext.GuestOrganizationId, TeamId = team.Id,
                    Priority = TicketPriority.Normal, DueAt = DateTime.UtcNow.AddHours(Ticket.TargetHours(TicketPriority.Normal)) };
                db.Tickets.Add(ticket);
            }
            var isNew = ticket.Id == 0;
            ticket.Messages.Add(new TicketMessage { AuthorId = ticket.RequesterId, AuthorName = session.Name, Body = body, Visibility = MessageVisibility.Public, Source = "Guest" });
            ticket.Events.Add(new TicketEvent { ActorId = ticket.RequesterId, ActorName = session.Name, Kind = isNew ? "Created" : "CustomerReply", Detail = "Solicitud desde portal sin cuenta; correo verificado." });
            await db.SaveChangesAsync(http.RequestAborted);
            foreach (var file in attachments) { file.TicketId = ticket.Id; db.TicketAttachments.Add(file); }
            if (isNew) await mail.ReceiptAsync(ticket);
            locked.ExpiresAt = DateTime.UtcNow;
            await db.SaveChangesAsync(http.RequestAborted);
            await tx.CommitAsync(http.RequestAborted);
            return Results.Ok(new { number = EmailComposer.Number(ticket.Id), existing = !isNew });
        }).RequireRateLimiting("guest");
    }
}
