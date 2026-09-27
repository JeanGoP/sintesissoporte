using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
namespace Sidecil.Tickets.Api;

public static class SupportCatalog
{
    public static IQueryable<SupportModule> Active(TicketsDbContext db) => db.SupportModules.Where(m => m.Enabled && db.SupportCategories.Any(c => c.Id == m.CategoryId && c.Enabled));
    public static async Task<SupportModule?> Resolve(TicketsDbContext db, Guid? id, string category) => await Active(db).FirstOrDefaultAsync(m => m.Id == id && db.SupportCategories.Any(c => c.Id == m.CategoryId && c.Name == category));
    public static bool ValidCompany(string? name) => name?.Trim().Length is >= 2 and <= 120;
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/catalog").RequireAuthorization().AddEndpointFilter<ValidationFilter>();
        admin.AddEndpointFilter(async (ctx, next) => (await ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>().GetUserAsync(ctx.HttpContext.User))?.Role == "Admin" ? await next(ctx) : Results.Forbid());
        admin.MapGet("", async (TicketsDbContext db) => Results.Ok(new { categories = await db.SupportCategories.OrderBy(x => x.Name).ToListAsync(), modules = await db.SupportModules.OrderBy(x => x.Name).ToListAsync() }));
        admin.MapPost("/categories", async (CategoryRequest r, TicketsDbContext db) =>
        {
            var name = r.Name.Trim(); if (name.Length < 2) return Results.BadRequest();
            if (await db.SupportCategories.AnyAsync(x => x.Name == name)) return Results.Problem("Ya existe esa categoría.", statusCode: 409);
            var item = new SupportCategory { Name = name, Enabled = r.Enabled }; db.Add(item); await db.SaveChangesAsync(); return Results.Ok(item);
        });
        admin.MapPut("/categories/{id:guid}", async (Guid id, CategoryRequest r, TicketsDbContext db) =>
        {
            var item = await db.SupportCategories.FindAsync(id); if (item == null) return Results.NotFound();
            var name = r.Name.Trim(); if (name.Length < 2) return Results.BadRequest();
            if (await db.SupportCategories.AnyAsync(x => x.Id != id && x.Name == name)) return Results.Problem("Ya existe esa categoría.", statusCode: 409);
            item.Name = name; item.Enabled = r.Enabled; await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPost("/modules", async (ModuleRequest r, TicketsDbContext db) =>
        {
            if (!await db.SupportCategories.AnyAsync(x => x.Id == r.CategoryId && x.Enabled) || r.Name.Trim().Length < 2) return Results.BadRequest();
            if (await db.SupportModules.AnyAsync(x => x.CategoryId == r.CategoryId && x.Name == r.Name.Trim())) return Results.Problem("Ya existe ese módulo en la categoría.", statusCode: 409);
            var item = new SupportModule { Name = r.Name.Trim(), CategoryId = r.CategoryId, Enabled = r.Enabled }; db.Add(item); await db.SaveChangesAsync(); return Results.Ok(item);
        });
        admin.MapPut("/modules/{id:guid}", async (Guid id, ModuleRequest r, TicketsDbContext db) =>
        {
            var item = await db.SupportModules.FindAsync(id); if (item == null) return Results.NotFound();
            if (!await db.SupportCategories.AnyAsync(x => x.Id == r.CategoryId) || r.Name.Trim().Length < 2) return Results.BadRequest();
            if (await db.SupportModules.AnyAsync(x => x.Id != id && x.CategoryId == r.CategoryId && x.Name == r.Name.Trim())) return Results.Problem("Ya existe ese módulo en la categoría.", statusCode: 409);
            if (item.CategoryId != r.CategoryId && (await db.Tickets.AnyAsync(x => x.ModuleId == id) || await db.GuestSubmissions.AnyAsync(x => x.ModuleId == id) || await db.ChatConversations.AnyAsync(x => x.ModuleId == id))) return Results.Problem("Este módulo ya está en uso. Crea otro para una categoría diferente.", statusCode: 409);
            item.Name = r.Name.Trim(); item.CategoryId = r.CategoryId; item.Enabled = r.Enabled; await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPut("/users/{id}/scope", async (string id, UserScopeRequest r, TicketsDbContext db) =>
        {
            var user = await db.Users.FindAsync(id); if (user == null) return Results.NotFound();
            var modules = (r.ModuleIds ?? []).Distinct().ToArray(); var orgs = (r.OrganizationIds ?? []).Distinct().ToArray();
            if (modules.Length > 100 || orgs.Length > 100 || await db.SupportModules.CountAsync(x => modules.Contains(x.Id)) != modules.Length || await db.Organizations.CountAsync(x => orgs.Contains(x.Id)) != orgs.Length) return Results.BadRequest();
            await using var tx = await db.Database.BeginTransactionAsync();
            db.AgentModules.RemoveRange(await db.AgentModules.Where(x => x.UserId == id).ToListAsync());
            db.UserOrganizations.RemoveRange(await db.UserOrganizations.Where(x => x.UserId == id).ToListAsync());
            await db.SaveChangesAsync();
            if (user.Role == "Agent") db.AgentModules.AddRange(modules.Select(m => new AgentModule { UserId = id, ModuleId = m }));
            db.UserOrganizations.AddRange(orgs.Select(o => new UserOrganization { UserId = id, OrganizationId = o }));
            var assigned = await db.Tickets.Where(t => t.AssigneeId == id && user.Role == "Agent" && (t.ModuleId == null || !modules.Contains(t.ModuleId.Value))).ToListAsync();
            foreach (var ticket in assigned) { ticket.AssigneeId = null; ticket.UpdatedAt = DateTime.UtcNow; }
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.NoContent();
        });
    }
}
