using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
namespace Sidecil.Tickets.Api;

public static class AdministrationLifecycle
{
    // Identity ya comprueba este bloqueo en acceso local y proveedores externos.
    public static readonly DateTimeOffset DisabledUntil = DateTimeOffset.MaxValue;
    public record EnabledRequest(bool Enabled);
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapPut("/users/{id}/enabled", async (string id, EnabledRequest request, TicketsDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();
            if (user.Role != "Agent") return Results.Problem("Esta opción corresponde a cuentas de agentes.", statusCode: 400);
            user.LockoutEnabled = true;
            user.LockoutEnd = request.Enabled ? null : DisabledUntil;
            user.AccessFailedCount = 0;
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            if (!request.Enabled)
            {
                var tickets = await db.Tickets.Where(t => t.AssigneeId == id && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Cancelled).ToListAsync();
                if (tickets.Count > 0 && !await db.UserClaims.AnyAsync(c => c.UserId == id && c.ClaimType == "Sidecil.AgentHistory")) db.UserClaims.Add(new IdentityUserClaim<string> { UserId = id, ClaimType = "Sidecil.AgentHistory", ClaimValue = "AssignedTickets" });
                foreach (var ticket in tickets) { ticket.AssigneeId = null; ticket.UpdatedAt = DateTime.UtcNow; ticket.Events.Add(new TicketEvent { Kind = "AgentDisabled", Detail = "Responsable desactivado; ticket disponible para reasignación." }); }
                await db.ChatConversations.Where(c => c.IdentityProvider == "Portal" && c.IdentitySubject == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTime.UtcNow));
            }
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.NoContent();
        });
        admin.MapDelete("/users/{id}", async (string id, TicketsDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();
            if (user.Role != "Agent") return Results.Problem("Solo se pueden eliminar agentes sin historial desde esta opción.", statusCode: 400);
            if (await db.UserClaims.AnyAsync(c => c.UserId == id && c.ClaimType == "Sidecil.AgentHistory") || await db.Tickets.AnyAsync(t => t.RequesterId == id || t.AssigneeId == id) || await db.Messages.AnyAsync(m => m.AuthorId == id) || await db.Events.AnyAsync(e => e.ActorId == id) || await db.ChatConversations.AnyAsync(c => c.IdentityProvider == "Portal" && c.IdentitySubject == id))
                return Results.Problem("Este agente tiene historial en tickets o conversaciones. Desactívalo para conservar la trazabilidad.", statusCode: 409);
            db.Users.Remove(user);
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Problem("El agente tiene registros relacionados. Desactívalo en lugar de eliminarlo.", statusCode: 409); }
            await tx.CommitAsync(); return Results.NoContent();
        });
        admin.MapDelete("/modules/{id:guid}", async (Guid id, TicketsDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var item = await db.SupportModules.FindAsync(id); if (item == null) return Results.NotFound();
            if (await db.Tickets.AnyAsync(x => x.ModuleId == id) || await db.GuestSubmissions.AnyAsync(x => x.ModuleId == id) || await db.ChatConversations.AnyAsync(x => x.ModuleId == id)) return Results.Problem("Este módulo tiene tickets o solicitudes vinculadas. Puedes desactivarlo, pero no eliminarlo.", statusCode: 409);
            if (await db.AgentModules.AnyAsync(x => x.ModuleId == id)) return Results.Problem("El módulo está asignado a agentes. Retira esas asignaciones antes de eliminarlo, o desactívalo.", statusCode: 409);
            db.SupportModules.Remove(item);
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Problem("El módulo está en uso. Actualiza la página y revisa sus relaciones.", statusCode: 409); }
            await tx.CommitAsync(); return Results.NoContent();
        });
        admin.MapDelete("/categories/{id:guid}", async (Guid id, TicketsDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var item = await db.SupportCategories.FindAsync(id); if (item == null) return Results.NotFound();
            if (await db.SupportModules.AnyAsync(x => x.CategoryId == id)) return Results.Problem("La categoría tiene módulos. Elimina primero los módulos sin uso o desactiva la categoría.", statusCode: 409);
            if (await db.Tickets.AnyAsync(x => x.Category == item.Name) || await db.GuestSubmissions.AnyAsync(x => x.Category == item.Name) || await db.ChatConversations.AnyAsync(x => x.Category == item.Name)) return Results.Problem("La categoría tiene solicitudes históricas. Desactívala para conservar el historial.", statusCode: 409);
            db.SupportCategories.Remove(item); await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.NoContent();
        });
    }
}
