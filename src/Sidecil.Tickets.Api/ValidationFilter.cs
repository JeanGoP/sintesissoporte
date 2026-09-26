using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sidecil.Tickets.Api;
public sealed class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var errors = new List<ValidationResult>();
        foreach (var argument in context.Arguments.Where(x => x is not null &&
                     x.GetType().Namespace == "Sidecil.Tickets.Application"))
            Validator.TryValidateObject(argument!, new ValidationContext(argument!), errors, true);
        if (errors.Count > 0) return Results.Problem(string.Join(" ", errors.Select(x => x.ErrorMessage)), statusCode: 400, title: "Revisa los datos");
        try { return await next(context); }
        catch (DbUpdateConcurrencyException) { return Results.Problem("Otro usuario modificó este ticket. Actualiza antes de guardar; tu texto no se perderá.", statusCode: 412); }
    }
}
