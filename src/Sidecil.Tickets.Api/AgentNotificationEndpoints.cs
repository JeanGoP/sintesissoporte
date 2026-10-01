using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Infrastructure;

namespace Sidecil.Tickets.Api;

public record NotificationSoundRequest(string Sound);

public static class AgentNotificationEndpoints
{
    private static readonly string[] Sounds = ["Off", "Chime", "Bell", "Soft"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPut("/auth/notification-sound", async (NotificationSoundRequest request, HttpContext context,
            UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null) return Results.Unauthorized();
            if (user.Role is not ("Admin" or "Agent")) return Results.Forbid();
            if (!Sounds.Contains(request.Sound, StringComparer.Ordinal))
                return Results.Problem("Selecciona un sonido válido.", statusCode: 400);
            user.NotificationSound = request.Sound;
            var result = await users.UpdateAsync(user);
            return result.Succeeded ? Results.NoContent() : Results.Problem("No se pudo guardar la preferencia.", statusCode: 409);
        });

        api.MapGet("/notifications", async (long? after, HttpContext context, TicketsDbContext db,
            UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null) return Results.Unauthorized();
            if (user.Role is not ("Admin" or "Agent")) return Results.Forbid();
            context.Response.Headers.CacheControl = "private, no-store";
            if (after is null)
            {
                var latest = await db.Events.MaxAsync(e => (long?)e.Id) ?? 0;
                return Results.Ok(new { cursor = latest, items = Array.Empty<object>() });
            }
            if (after < 0) return Results.Problem("Cursor inválido.", statusCode: 400);
            var newest = await db.Events.MaxAsync(e => (long?)e.Id) ?? 0;
            if (newest <= after.Value) return Results.Ok(new { cursor = after.Value, items = Array.Empty<object>() });

            var visible = user.Role == "Admin" ? db.Tickets : db.Tickets.Where(t => t.ModuleId != null &&
                (t.AssigneeId == null || t.AssigneeId == user.Id) &&
                db.AgentModules.Any(m => m.UserId == user.Id && m.ModuleId == t.ModuleId));
            var entries = await (from e in db.Events.AsNoTracking()
                join t in visible on e.TicketId equals t.Id
                where e.Id > after.Value && e.Id <= newest && (e.ActorId == null || e.ActorId != user.Id) &&
                    (e.Kind == "Created" || e.Kind == "CustomerReply" || e.Kind == "EmailReply" ||
                     (e.Kind == "Reply" && e.ActorId != null && e.ActorId == t.RequesterId))
                orderby e.Id
                select new { eventId = e.Id, e.Kind, t.PublicId, t.Id, t.Subject }).Take(25).ToListAsync();
            return Results.Ok(new
            {
                cursor = entries.Count == 25 ? entries[^1].eventId : newest,
                items = entries.Select(e => new
                {
                    e.eventId,
                    ticketId = e.PublicId,
                    number = $"SC-{e.Id:00000}",
                    e.Subject,
                    kind = e.Kind == "Created" ? "ticket" : "reply"
                })
            });
        });
    }
}
