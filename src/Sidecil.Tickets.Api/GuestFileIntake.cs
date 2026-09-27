using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;
namespace Sidecil.Tickets.Api;
public static class GuestFileIntake
{
    public static async Task<IResult> SubmitAsync(HttpContext http, TicketsDbContext db, EmailComposer mail, IOptions<MailOptions> options)
    {
        IResult Bad(string message, int status = 400) => Results.Problem(message, statusCode: status);
        if (options.Value.Mode == "Disabled") return Bad("La recepción de solicitudes sin cuenta no está habilitada.", 503);
        if (!http.Request.HasFormContentType) return Bad("Envía el formulario con sus archivos.");
        const long maxRequest = 16 * 1024 * 1024;
        if (http.Request.ContentLength > maxRequest) return Bad("Los archivos superan el tamaño permitido.", 413);
        var limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = maxRequest;
        try {
            var form = await http.Request.ReadFormAsync(new FormOptions { MemoryBufferThreshold = ChatRules.MaxFileBytes, MultipartBodyLengthLimit = ChatRules.MaxFileBytes, ValueLengthLimit = 16000, ValueCountLimit = 10 }, http.RequestAborted);
            var request = new GuestTicketRequest(form["name"].ToString(), form["email"].ToString(), form["subject"].ToString(), form["body"].ToString(), form["category"].ToString());
            if (!Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true)) return Bad("Revisa tu nombre, correo, asunto y descripción.");
            if (form.Files.Count > ChatRules.MaxFiles) return Bad("Puedes adjuntar hasta 3 archivos.");
            var files = new List<GuestAttachment>();
            foreach (var file in form.Files) {
                if (file.Length is <= 0 or > ChatRules.MaxFileBytes) return Bad("Cada archivo debe pesar entre 1 byte y 5 MB.", 413);
                var name = ChatRules.SafeName(file.FileName);
                if (string.IsNullOrWhiteSpace(name) || name.Length > 180) return Bad("El nombre del archivo no es válido o es demasiado largo.");
                using var memory = new MemoryStream(); await file.CopyToAsync(memory, http.RequestAborted);
                var content = memory.ToArray(); var type = ChatRules.FileType(name, content);
                if (type is null) return Bad("Adjunta un PNG, JPG, PDF o TXT válido de hasta 5 MB.");
                files.Add(new GuestAttachment { FileName = name, ContentType = type, Length = content.Length, Content = content });
            }
            return await EmailEndpoints.SubmitAsync(request, db, mail, options, files);
        } catch (InvalidDataException) { return Bad("No se pudo leer el formulario. Adjunta hasta 3 archivos de máximo 5 MB.", 413); }
        catch (BadHttpRequestException ex) when (ex.StatusCode == 413) { return Bad("Los archivos superan el tamaño permitido.", 413); }
    }
}
