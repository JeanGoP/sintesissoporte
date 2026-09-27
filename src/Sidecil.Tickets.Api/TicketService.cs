using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

namespace Sidecil.Tickets.Api;

public sealed class TicketService(TicketsDbContext db, UserManager<ApplicationUser> users, EmailComposer mail, UserInvitations invitations)
{
    private async Task<ApplicationUser> Actor(HttpContext c) => (await users.GetUserAsync(c.User))!;
    private IQueryable<Ticket> Visible(ApplicationUser u) => u.Role switch {
        "Admin" => db.Tickets,
        "Agent" => db.Tickets.Where(t => u.TeamId != null && t.TeamId == u.TeamId),
        _ => db.Tickets.Where(t => t.RequesterId == u.Id && t.OrganizationId == u.OrganizationId)
    };
    private static bool Staff(ApplicationUser u) => AccessRules.IsStaff(u.Role);
    private static IResult Bad(string detail, int status = 400) => Results.Problem(detail, statusCode: status);
    private static bool Matches(HttpContext c, Ticket t) => c.Request.Headers.IfMatch.ToString() == Etag(t);
    private static string Etag(Ticket t) => "\"" + Convert.ToBase64String(t.Version) + "\"";
    private static void Event(Ticket t, ApplicationUser u, string kind, string detail) =>
        t.Events.Add(new TicketEvent { ActorId = u.Id, Kind = kind, Detail = detail });
    private static readonly string[] Categories = ["General", "Soporte técnico", "Facturación", "Accesos", "Servicios"];
    private static string Number(long id) => $"SC-{id:00000}";

    public async Task<IResult> ListAsync(HttpContext c, string? search, string? status, string? priority, string? view, int page)
    {
        var actor = await Actor(c);
        var visible = Visible(actor);
        var now = DateTime.UtcNow;
        var active = visible.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled);
        var summary = new {
            active = await active.CountAsync(),
            unassigned = await active.CountAsync(t => t.AssigneeId == null),
            overdue = await active.CountAsync(t => t.DueAt < now),
            resolved = await visible.CountAsync(t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed)
        };
        var query = visible;
        if (!string.IsNullOrWhiteSpace(search)) {
            search = search.Trim();
            if (search.Length > 180) return Bad("La búsqueda admite hasta 180 caracteres.");
            if (long.TryParse(search.Replace("SC-", "", StringComparison.OrdinalIgnoreCase), out var number))
                query = query.Where(t => t.Id == number);
            else query = query.Where(t => t.Subject.Contains(search));
        }
        if (!string.IsNullOrEmpty(status)) {
            if (!Enum.TryParse<TicketStatus>(status, out var parsed) || !Enum.IsDefined(parsed)) return Bad("Estado inválido.");
            query = query.Where(t => t.Status == parsed);
        }
        if (!string.IsNullOrEmpty(priority)) {
            if (!Enum.TryParse<TicketPriority>(priority, out var parsed) || !Enum.IsDefined(parsed)) return Bad("Prioridad inválida.");
            query = query.Where(t => t.Priority == parsed);
        }
        query = view switch {
            "replies" => query.Where(t => t.HasCustomerReply),
            "mine" => query.Where(t => t.AssigneeId == actor.Id),
            "unassigned" => query.Where(t => t.AssigneeId == null && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled),
            "overdue" => query.Where(t => t.DueAt < now && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled),
            _ => query
        };
        page = Math.Clamp(page, 1, 10000);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id).Skip((page - 1) * 25).Take(25)
            .Select(t => new { t.Id, t.PublicId, t.Subject, t.Category, t.Status, t.Priority, t.CreatedAt, t.UpdatedAt, t.DueAt, t.HasCustomerReply,
                requester = db.Users.Where(u => u.Id == t.RequesterId).Select(u => u.DisplayName).FirstOrDefault() ?? t.GuestName!,
                organization = db.Organizations.Where(o => o.Id == t.OrganizationId).Select(o => o.Name).First(),
                assignee = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault()
            }).ToListAsync();
        return Results.Ok(new { items = rows.Select(t => new { number = Number(t.Id), id = t.PublicId, t.Subject, t.Category, t.Status, t.Priority, t.CreatedAt, t.UpdatedAt, t.DueAt, t.HasCustomerReply, t.requester, t.organization, t.assignee }), total, page, pageSize = 25, summary });
    }

    public async Task<IResult> DetailAsync(HttpContext c, Guid id)
    {
        var actor = await Actor(c);
        var ticket = await Visible(actor).AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == id);
        if (ticket is null) return Results.NotFound();
        c.Response.Headers.ETag = Etag(ticket);
        c.Response.Headers.CacheControl = "private, no-store";
        var staff = Staff(actor);
        var messages = await db.Messages.Where(m => m.TicketId == ticket.Id && (staff || m.Visibility == MessageVisibility.Public))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Select(m => new { m.Id, m.Body, m.Visibility, m.CreatedAt, m.Source,
                author = db.Users.Where(u => u.Id == m.AuthorId).Select(u => u.DisplayName).FirstOrDefault() ?? m.AuthorName!,
                own = m.AuthorId == actor.Id }).ToListAsync();
        // El historial operativo puede contener motivos privados; solo se expone a agentes autorizados.
        var events = staff ? await db.Events.Where(e => e.TicketId == ticket.Id).OrderByDescending(e => e.Id)
            .Select(e => new { e.Id, e.Kind, e.Detail, e.CreatedAt, actor = db.Users.Where(u => u.Id == e.ActorId).Select(u => u.DisplayName).FirstOrDefault() ?? e.ActorName! }).ToListAsync() : null;
        var requester = await db.Users.Where(u => u.Id == ticket.RequesterId).Select(u => new { u.DisplayName, u.Email }).FirstOrDefaultAsync()
            ?? new { DisplayName = ticket.GuestName!, Email = ticket.GuestEmail };
        var organization = await db.Organizations.Where(o => o.Id == ticket.OrganizationId).Select(o => o.Name).FirstAsync();
        return Results.Ok(new {
            version = Etag(ticket), id = ticket.PublicId, number = Number(ticket.Id), ticket.Subject, ticket.Category, ticket.Status, ticket.Priority,
            ticket.CreatedAt, ticket.UpdatedAt, ticket.DueAt, ticket.HasCustomerReply, ticket.ResolvedAt, ticket.AssigneeId, requester, organization,
            attachments = await (from file in db.ChatAttachments
                join chat in db.ChatConversations on file.ConversationId equals chat.Id
                join pending in db.GuestSubmissions on chat.GuestSubmissionId equals pending.Id
                where pending.TicketId == ticket.Id
                select new { file.Id, file.FileName, file.Length }).ToListAsync(),
            messages, events, nextStatuses = staff ? Ticket.NextStatuses(ticket.Status) : []
        });
    }

    public async Task<IResult> AttachmentAsync(HttpContext c, Guid id, Guid fileId)
    {
        var actor = await Actor(c);
        var ticket = await Visible(actor).AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == id);
        if (ticket is null) return Results.NotFound();
        var file = await (from attachment in db.ChatAttachments.AsNoTracking()
            join chat in db.ChatConversations on attachment.ConversationId equals chat.Id
            join pending in db.GuestSubmissions on chat.GuestSubmissionId equals pending.Id
            where attachment.Id == fileId && pending.TicketId == ticket.Id
            select attachment).FirstOrDefaultAsync();
        if (file is null) return Results.NotFound();
        return Results.File(file.Content, "application/octet-stream", file.FileName);
    }

    public async Task<IResult> CreateAsync(HttpContext c, CreateTicketRequest request)
    {
        if (!Enum.IsDefined(request.Priority) || !Categories.Contains(request.Category)) return Bad("Categoría o prioridad inválida.");
        var actor = await Actor(c);
        var team = await db.Teams.OrderBy(t => t.Name).FirstOrDefaultAsync();
        if (team is null) return Bad("No hay un equipo de atención configurado.", 409);
        var ticket = new Ticket {
            Subject = request.Subject.Trim(), Category = request.Category, OrganizationId = actor.OrganizationId,
            TeamId = team.Id, RequesterId = actor.Id, Priority = request.Priority,
            DueAt = DateTime.UtcNow.AddHours(Ticket.TargetHours(request.Priority))
        };
        if (ticket.Subject.Length < 5 || request.Body.Trim().Length < 10) return Bad("Escribe un asunto y una descripción más detallados.");
        ticket.Messages.Add(new TicketMessage { AuthorId = actor.Id, Body = request.Body.Trim(), Visibility = MessageVisibility.Public });
        Event(ticket, actor, "Created", "Solicitud creada");
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        await mail.ReceiptAsync(ticket);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/api/v1/tickets/{ticket.PublicId}", new { id = ticket.PublicId, number = Number(ticket.Id) });
    }

    public async Task<IResult> MessageAsync(HttpContext c, Guid id, AddMessageRequest request)
    {
        var actor = await Actor(c);
        var ticket = await Visible(actor).FirstOrDefaultAsync(t => t.PublicId == id);
        if (ticket is null) return Results.NotFound();
        if (!Enum.IsDefined(request.Visibility) || request.Visibility == MessageVisibility.Internal && !Staff(actor)) return Results.Forbid();
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled) return Bad("Este ticket no admite mensajes. Un agente debe reabrirlo cuando corresponda.", 409);
        if (!Matches(c, ticket)) return Bad("El ticket cambió. Actualiza antes de enviar; conserva tu borrador.", 412);
        if (string.IsNullOrWhiteSpace(request.Body)) return Bad("Escribe un mensaje.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var message = new TicketMessage { AuthorId = actor.Id, Body = request.Body.Trim(), Visibility = request.Visibility };
        ticket.Messages.Add(message);
        if (request.Visibility == MessageVisibility.Public) ticket.HasCustomerReply = !Staff(actor);
        ticket.UpdatedAt = DateTime.UtcNow;
        Event(ticket, actor, request.Visibility == MessageVisibility.Internal ? "InternalNote" : "Reply", request.Visibility == MessageVisibility.Internal ? "Nota interna añadida" : "Respuesta pública añadida");
        await db.SaveChangesAsync();
        if (Staff(actor) && request.Visibility == MessageVisibility.Public && actor.Id != ticket.RequesterId)
            await mail.AgentReplyAsync(ticket, message);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.NoContent();
    }

    public async Task<IResult> TransitionAsync(HttpContext c, Guid id, TransitionRequest request)
    {
        var actor = await Actor(c);
        if (!Staff(actor)) return Results.Forbid();
        var ticket = await Visible(actor).FirstOrDefaultAsync(t => t.PublicId == id);
        if (ticket is null) return Results.NotFound();
        if (!Matches(c, ticket)) return Bad("El ticket cambió. Actualiza antes de guardar.", 412);
        var before = ticket.Status;
        try { ticket.Transition(request.Status, request.Reason.Trim(), DateTime.UtcNow); }
        catch (InvalidOperationException ex) { return Bad(ex.Message, 409); }
        Event(ticket, actor, "StatusChanged", $"{before} → {request.Status}: {request.Reason.Trim()}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    public async Task<IResult> AssignAsync(HttpContext c, Guid id, AssignmentRequest request)
    {
        var actor = await Actor(c);
        if (!Staff(actor)) return Results.Forbid();
        var ticket = await Visible(actor).FirstOrDefaultAsync(t => t.PublicId == id);
        if (ticket is null) return Results.NotFound();
        if (!Matches(c, ticket)) return Bad("El ticket cambió. Actualiza antes de asignar.", 412);
        ApplicationUser? assignee = null;
        if (request.AssigneeId is not null) {
            assignee = await db.Users.FirstOrDefaultAsync(u => u.Id == request.AssigneeId &&
                (u.Role == "Admin" || u.Role == "Agent" && u.TeamId == ticket.TeamId));
            if (assignee is null) return Bad("El responsable no pertenece al equipo autorizado.");
        }
        ticket.AssigneeId = request.AssigneeId;
        ticket.UpdatedAt = DateTime.UtcNow;
        Event(ticket, actor, "Assigned", assignee is null ? "Ticket sin asignar" : $"Asignado a {assignee.DisplayName}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    public async Task<IResult> DirectoryAsync(HttpContext c)
    {
        var actor = await Actor(c);
        var staff = Staff(actor);
        return Results.Ok(new {
            categories = Categories,
            agents = staff ? await db.Users.Where(u => u.Role == "Admin" || u.Role == "Agent" && (actor.Role == "Admin" || u.TeamId == actor.TeamId))
                .Select(u => new { u.Id, u.DisplayName, u.TeamId }).ToListAsync() : null,
            organizations = actor.Role == "Admin" ? await db.Organizations.OrderBy(o => o.Name).ToListAsync() : null,
            teams = actor.Role == "Admin" ? await db.Teams.OrderBy(t => t.Name).ToListAsync() : null,
            users = actor.Role == "Admin" ? await db.Users.OrderBy(u => u.DisplayName).Select(u => new { u.Id, u.DisplayName, u.Email, u.Role, u.OrganizationId, u.TeamId, invitationPending = u.PasswordHash == null && !db.UserLogins.Any(l => l.UserId == u.Id) }).ToListAsync() : null
        });
    }

    public async Task<IResult> CreateUserAsync(HttpContext c, CreateUserRequest request)
    {
        if ((await Actor(c)).Role != "Admin") return Results.Forbid();
        if (request.Role is not ("Requester" or "Agent" or "Admin")) return Bad("Rol inválido.");
        if (!await db.Organizations.AnyAsync(o => o.Id == request.OrganizationId)) return Bad("Organización inválida.");
        if (request.Role == "Agent" && (request.TeamId is null || !await db.Teams.AnyAsync(t => t.Id == request.TeamId))) return Bad("Selecciona un equipo válido.");
        if (!invitations.Enabled) return Bad("Configura el correo antes de invitar usuarios.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser {
            UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim(),
            Role = request.Role, OrganizationId = request.OrganizationId, TeamId = request.Role == "Agent" ? request.TeamId : null
        };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded) return Bad("No se pudo crear el usuario. Verifica que el correo sea válido y no esté registrado.");
        await invitations.QueueAsync(user);
        await tx.CommitAsync();
        return Results.Created("/api/v1/directory", new { user.Id, invitationQueued = true });
    }

    public async Task<IResult> CreateOrganizationAsync(HttpContext c, CreateOrganizationRequest request)
    {
        if ((await Actor(c)).Role != "Admin") return Results.Forbid();
        var name = request.Name.Trim();
        if (name.Length < 2) return Bad("Escribe un nombre válido.");
        if (await db.Organizations.AnyAsync(o => o.Name == name)) return Bad("Ya existe esa organización.", 409);
        var organization = new Organization { Name = name };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();
        return Results.Created("/api/v1/directory", organization);
    }
}
