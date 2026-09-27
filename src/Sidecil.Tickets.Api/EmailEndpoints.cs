using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

namespace Sidecil.Tickets.Api;
public sealed class EmailEndpoints
{
    public static async Task<IResult> SubmitAsync(GuestTicketRequest request, TicketsDbContext db, EmailComposer composer, IOptions<MailOptions> options, IReadOnlyList<GuestAttachment>? files = null) {
            if (options.Value.Mode == "Disabled") return Results.Problem("La recepción de solicitudes sin cuenta no está habilitada.", statusCode: 503);
            if (!new[] { "General", "Soporte técnico", "Facturación", "Accesos", "Servicios" }.Contains(request.Category))
                return Results.Problem("Categoría inválida.", statusCode: 400);
            var now = DateTime.UtcNow;
            var email = request.Email.Trim().ToLowerInvariant();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.GuestSubmissions.CountAsync(x => x.Email == email && x.CreatedAt > now.AddHours(-1)) >= 3)
                return files is { Count: > 0 } ? Results.Problem("Ya recibimos varias solicitudes para este correo. Espera una hora antes de enviar otra con archivos.", statusCode: 429) : Results.Accepted(value: new { message = "Revisa tu correo y confirma la solicitud. Si ya solicitaste varios enlaces, utiliza el más reciente." });
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var pending = new GuestSubmission {
                TokenHash = EmailComposer.Hash(token), Name = request.Name.Trim(), Email = email,
                Subject = request.Subject.Trim(), Body = request.Body.Trim(), Category = request.Category, ExpiresAt = now.AddHours(24)
            };
            db.GuestSubmissions.Add(pending);
            if (files is not null) foreach (var file in files) { file.GuestSubmissionId = pending.Id; db.GuestAttachments.Add(file); }
            var link = options.Value.PublicBaseUrl.TrimEnd('/') + "/confirmar-solicitud#token=" + token;
            composer.Queue(email, "Confirma tu solicitud a Sidecil",
                "Recibimos una solicitud de soporte asociada a esta dirección.\n\nPara verificar que el correo es tuyo y registrar el ticket, abre este enlace y pulsa Confirmar:\n" + link +
                "\n\nEl enlace caduca en 24 horas. Si no realizaste la solicitud, ignora este mensaje. No necesitas crear una cuenta.",
                "verify:" + pending.Id, "Verification", expires: pending.ExpiresAt);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Accepted(value: new { message = "Revisa tu correo y confirma la solicitud. Luego recibirás tu número de ticket." });

    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/public/config", (IOptions<MailOptions> options) => Results.Ok(new {
            available = options.Value.Mode != "Disabled", testMode = options.Value.Mode == "Pickup",
            categories = new[] { "General", "Soporte técnico", "Facturación", "Accesos", "Servicios" }
        }));
        app.MapPost("/api/v1/public/tickets", (GuestTicketRequest request, TicketsDbContext db, EmailComposer composer, IOptions<MailOptions> options) => SubmitAsync(request, db, composer, options)).AddEndpointFilter<ValidationFilter>().RequireRateLimiting("guest");
        app.MapPost("/api/v1/public/tickets/with-attachments", GuestFileIntake.SubmitAsync).RequireRateLimiting("guest");

        app.MapPost("/api/v1/public/confirm", async (ConfirmGuestRequest request, TicketsDbContext db, EmailComposer composer) => {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var hash = EmailComposer.Hash(request.Token);
            var pending = await db.GuestSubmissions.SingleOrDefaultAsync(x => x.TokenHash == hash);
            if (pending is null || pending.ExpiresAt < DateTime.UtcNow) return Results.Problem("El enlace no es válido o ya venció. Envía una nueva solicitud.", statusCode: 400);
            if (pending.TicketId is { } existing) return Results.Ok(new { number = EmailComposer.Number(existing) });
            var team = await db.Teams.OrderBy(x => x.Name).FirstOrDefaultAsync();
            if (team is null) return Results.Problem("No hay equipo de atención disponible.", statusCode: 409);
            var ticket = new Ticket {
                Subject = pending.Subject, Category = pending.Category, GuestName = pending.Name, GuestEmail = pending.Email,
                OrganizationId = TicketsDbContext.GuestOrganizationId, TeamId = team.Id, Priority = TicketPriority.Normal,
                DueAt = DateTime.UtcNow.AddHours(Ticket.TargetHours(TicketPriority.Normal))
            };
            var fromChat = await db.ChatConversations.AnyAsync(x => x.GuestSubmissionId == pending.Id);
            ticket.Messages.Add(new TicketMessage { AuthorName = pending.Name, Body = pending.Body, Source = fromChat ? "Chat" : "Guest" });
            ticket.Events.Add(new TicketEvent { ActorName = pending.Name, Kind = "Created", Detail = "Solicitud sin cuenta; correo confirmado." });
            db.Tickets.Add(ticket); await db.SaveChangesAsync();
            pending.TicketId = ticket.Id;
            var files = await db.GuestAttachments.Where(f => f.GuestSubmissionId == pending.Id).ToListAsync();
            foreach (var file in files) db.TicketAttachments.Add(new TicketAttachment { Id = file.Id, TicketId = ticket.Id, FileName = file.FileName, ContentType = file.ContentType, Length = file.Length, Content = file.Content });
            db.GuestAttachments.RemoveRange(files);
            // Clear the draft after materializing the ticket; preserve token hash for idempotent confirmation.
            pending.Body = ""; pending.Subject = "";
            await composer.ReceiptAsync(ticket);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Results.Ok(new { number = EmailComposer.Number(ticket.Id) });
        }).AddEndpointFilter<ValidationFilter>().RequireRateLimiting("login");

        var admin = app.MapGroup("/api/v1/admin/email").RequireAuthorization().AddEndpointFilter<ValidationFilter>();
        admin.AddEndpointFilter(async (context, next) => {
            var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.GetUserAsync(context.HttpContext.User);
            return user?.Role == "Admin" ? await next(context) : Results.Forbid();
        });
        admin.MapGet("", async (TicketsDbContext db, IOptions<MailOptions> options) => {
            var t = await db.EmailTemplates.AsNoTracking().SingleAsync(x => x.Id == 1);
            return Results.Ok(new { template = new { t.Subject, t.Body, t.Signature, version = Convert.ToBase64String(t.Version) },
                mode = options.Value.Mode,
                outbound = await db.OutboundEmails.OrderByDescending(x => x.CreatedAt).Take(50)
                    .Select(x => new { x.Id, x.TicketId, x.Recipient, x.Subject, x.State, x.Kind, x.Attempts, x.CreatedAt, x.SentAt, x.LastError }).ToListAsync(),
                inbound = await db.IncomingEmails.OrderByDescending(x => x.ReceivedAt).Take(50)
                    .Select(x => new { x.Id, x.Sender, x.Subject, x.State, x.Reason, x.ReceivedAt, x.TicketId }).ToListAsync()
            });
        });
        admin.MapPut("/template", async (EmailTemplateRequest request, TicketsDbContext db, HttpContext c, UserManager<ApplicationUser> users) => {
            if (!EmailComposer.ValidTemplate(request.Subject) || !EmailComposer.ValidTemplate(request.Body) ||
                request.Subject.Contains('\r') || request.Subject.Contains('\n'))
                return Results.Problem("Usa solo {nombre}, {numero}, {asunto} y {firma}. El asunto debe ocupar una línea.", statusCode: 400);
            if (EmailComposer.Render(request.Body, new string('N', 120), "SC-9223372036854775807", new string('A', 180), request.Signature).Length > 15500 ||
                EmailComposer.Render(request.Subject, new string('N', 120), "SC-9223372036854775807", new string('A', 180), request.Signature).Length > 470)
                return Results.Problem("La plantilla expandida es demasiado larga. Reduce el texto o la repetición de variables.", statusCode: 400);
            var t = await db.EmailTemplates.SingleAsync(x => x.Id == 1);
            if (Convert.ToBase64String(t.Version) != request.Version) return Results.Problem("La plantilla cambió. Recarga antes de guardar.", statusCode: 412);
            t.Subject = request.Subject.Trim(); t.Body = request.Body.Trim(); t.Signature = request.Signature.Trim();
            t.UpdatedAt = DateTime.UtcNow; t.UpdatedBy = (await users.GetUserAsync(c.User))!.Id;
            await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPost("/{id:guid}/retry", async (Guid id, TicketsDbContext db) => {
            var changed = await db.OutboundEmails.Where(x => x.Id == id && x.State == "Failed" && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Pending").SetProperty(x => x.Attempts, 0)
                    .SetProperty(x => x.NextAttemptAt, DateTime.UtcNow).SetProperty(x => x.LastError, (string?)null));
            return changed == 1 ? Results.NoContent() : Results.Problem("Solo se pueden reintentar mensajes fallidos que no hayan caducado.", statusCode: 409);
        });
    }
}
