using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

namespace Sidecil.Tickets.Api;

public record AcceptInvitationRequest(
    [property: Required, StringLength(450)] string UserId,
    [property: Required, StringLength(4096)] string Token,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password);

public sealed class UserInvitations(TicketsDbContext db, UserManager<ApplicationUser> users,
    EmailComposer mail, IOptions<MailOptions> options, IConfiguration config)
{
    public bool Enabled => options.Value.Mode != "Disabled";
    public async Task QueueAsync(ApplicationUser user)
    {
        var token = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, "Invitation");
        var origin = config["Frontend:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(origin)) origin = options.Value.PublicBaseUrl;
        // El fragmento no se envía al servidor web ni aparece en registros de acceso.
        var link = origin.TrimEnd('/') + "/activar-cuenta#user=" + Uri.EscapeDataString(user.Id) + "&token=" + Uri.EscapeDataString(token);
        mail.Queue(user.Email!, "Te invitamos a Sidecil Soporte",
            $"Hola {user.DisplayName},\n\nTe han creado una cuenta en Sidecil Soporte. Define tu contraseña en este enlace:\n\n{link}\n\nEl enlace es de un solo uso y vence en 24 horas. Si vence, solicita al administrador una nueva invitación.\n\nEquipo Sidecil",
            "invitation:" + Guid.NewGuid(), "Invitation", expires: DateTime.UtcNow.AddHours(24));
        await db.SaveChangesAsync();
    }
    public async Task<IResult> ResendAsync(string id, HttpContext http)
    {
        if ((await users.GetUserAsync(http.User))?.Role != "Admin") return Results.Forbid();
        if (!Enabled) return Results.Problem("Configura el correo antes de enviar invitaciones.", statusCode: 400);
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await users.FindByIdAsync(id);
        if (user is null || user.LockoutEnd == AdministrationLifecycle.DisabledUntil || await users.HasPasswordAsync(user) || (await users.GetLoginsAsync(user)).Count != 0)
            return Results.Problem("Esta cuenta no tiene una invitación pendiente.", statusCode: 400);
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded) return Results.Conflict();
        await QueueAsync(user);
        await tx.CommitAsync();
        return Results.NoContent();
    }
    public async Task<IResult> AcceptAsync(AcceptInvitationRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || user.LockoutEnd == AdministrationLifecycle.DisabledUntil || await users.HasPasswordAsync(user) || (await users.GetLoginsAsync(user)).Count != 0 ||
            !await users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "Invitation", request.Token))
            return Results.Problem("El enlace no es válido, venció o ya fue utilizado. Solicita una nueva invitación al administrador.", statusCode: 400);
        var result = await users.AddPasswordAsync(user, request.Password);
        if (!result.Succeeded) return Results.Problem("No se pudo crear la contraseña. Usa al menos 12 caracteres, mayúsculas, minúsculas, números y símbolos. Si persiste, solicita una nueva invitación.", statusCode: 400);
        user.EmailConfirmed = true;
        if (!(await users.UpdateAsync(user)).Succeeded) return Results.Conflict();
        await tx.CommitAsync();
        return Results.NoContent();
    }
}
