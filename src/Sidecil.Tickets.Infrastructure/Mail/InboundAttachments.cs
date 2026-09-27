using MimeKit;
using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
namespace Sidecil.Tickets.Infrastructure.Mail;
public static class InboundAttachments
{
    // Allow base64 overhead for three 5 MiB files.
    public const int MaxMessageBytes = 24 * 1024 * 1024;
    public static List<TicketAttachment> Read(MimeMessage message, CancellationToken ct = default)
    {
        var files = new List<TicketAttachment>();
        foreach (var entity in message.BodyParts.Where(p => p.IsAttachment ||
                     p is MimePart part && (part.FileName is not null || part.ContentType.MediaType == "image"))) {
            if (files.Count == ChatRules.MaxFiles)
                throw new InvalidDataException("El correo supera los 3 archivos permitidos; revisar en el buzón.");
            if (entity is not MimePart file || file.Content is null)
                throw new InvalidDataException("El correo incluye un adjunto no compatible; revisar en el buzón.");
            var name = ChatRules.SafeName(file.FileName ?? ("imagen" + (files.Count + 1) + (file.ContentType.MimeType.ToLowerInvariant() switch {
                "image/png" => ".png", "image/jpeg" => ".jpg", _ => ".bin"
            })));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 180)
                throw new InvalidDataException("El nombre de un adjunto no es válido; revisar en el buzón.");
            using var buffer = new LimitedBuffer();
            file.Content.DecodeTo(buffer, ct);
            var content = buffer.ToArray();
            var type = ChatRules.FileType(name, content);
            if (type is null)
                throw new InvalidDataException("Adjunto no válido: se permiten PNG, JPG, PDF y TXT de hasta 5 MB; revisar en el buzón.");
            files.Add(new TicketAttachment { FileName = name, ContentType = type, Length = content.Length, Content = content });
        }
        return files;
    }
    private sealed class LimitedBuffer : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Length + count > ChatRules.MaxFileBytes)
                throw new InvalidDataException("Un adjunto supera los 5 MB permitidos; revisar en el buzón.");
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Length + buffer.Length > ChatRules.MaxFileBytes)
                throw new InvalidDataException("Un adjunto supera los 5 MB permitidos; revisar en el buzón.");
            base.Write(buffer);
        }
    }
}
