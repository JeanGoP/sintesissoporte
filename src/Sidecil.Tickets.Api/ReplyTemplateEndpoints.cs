using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;

namespace Sidecil.Tickets.Api;

public record ReplyTemplateRequest(string Title, string Body, bool Enabled);

public static class ReplyTemplateEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reply-templates", async (HttpContext context, TicketsDbContext db, UserManager<ApplicationUser> users) =>
        {
            var actor = await users.GetUserAsync(context.User);
            if (actor?.Role is not ("Admin" or "Agent")) return Results.Forbid();
            return Results.Ok(await db.ReplyTemplates.AsNoTracking().Where(t => t.Enabled)
                .OrderBy(t => t.Title).Select(t => new { t.Id, t.Title, t.Body }).ToListAsync());
        });

        var admin = api.MapGroup("/admin/reply-templates");
        admin.AddEndpointFilter(async (context, next) =>
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            return (await users.GetUserAsync(context.HttpContext.User))?.Role is "Admin" or "Agent"
                ? await next(context) : Results.Forbid();
        });
        admin.MapGet("", async (TicketsDbContext db) => Results.Ok(await db.ReplyTemplates.AsNoTracking()
            .OrderBy(t => t.Title).ToListAsync()));
        admin.MapPost("", async (ReplyTemplateRequest request, TicketsDbContext db) =>
        {
            var title = request.Title?.Trim() ?? "";
            var body = request.Body?.Trim() ?? "";
            if (!Valid(title, body)) return Results.Problem("Escribe un nombre de 2 a 80 caracteres y una respuesta de 5 a 4.000 caracteres.", statusCode: 400);
            if (await db.ReplyTemplates.AnyAsync(t => t.Title == title)) return Results.Problem("Ya existe una plantilla con ese nombre.", statusCode: 409);
            var template = new ReplyTemplate { Title = title, Body = body, Enabled = request.Enabled };
            db.ReplyTemplates.Add(template);
            await db.SaveChangesAsync();
            return Results.Created("/api/v1/admin/reply-templates/" + template.Id, template);
        });
        admin.MapPut("/{id:guid}", async (Guid id, ReplyTemplateRequest request, TicketsDbContext db) =>
        {
            var template = await db.ReplyTemplates.FindAsync(id);
            if (template is null) return Results.NotFound();
            var title = request.Title?.Trim() ?? "";
            var body = request.Body?.Trim() ?? "";
            if (!Valid(title, body)) return Results.Problem("Escribe un nombre de 2 a 80 caracteres y una respuesta de 5 a 4.000 caracteres.", statusCode: 400);
            if (await db.ReplyTemplates.AnyAsync(t => t.Id != id && t.Title == title)) return Results.Problem("Ya existe una plantilla con ese nombre.", statusCode: 409);
            template.Title = title;
            template.Body = body;
            template.Enabled = request.Enabled;
            template.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        admin.MapDelete("/{id:guid}", async (Guid id, TicketsDbContext db) =>
            await db.ReplyTemplates.Where(t => t.Id == id).ExecuteDeleteAsync() == 1 ? Results.NoContent() : Results.NotFound());
    }

    private static bool Valid(string title, string body) => title.Length is >= 2 and <= 80 && body.Length is >= 5 and <= 4000;
}
