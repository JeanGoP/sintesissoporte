using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using HtmlAgilityPack;
using System.Net;
using Sidecil.Tickets.Domain;

namespace Sidecil.Tickets.Infrastructure.Mail;
public sealed class InboundProcessor(TicketsDbContext db, EmailComposer composer, IOptions<MailOptions> options)
{
    public static bool Authenticated(MimeMessage message, string sender, string trustedService)
    {
        if (string.IsNullOrWhiteSpace(trustedService)) return false;
        // Only the FIRST header inserted by the configured receiving provider is trusted.
        // The provider must strip spoofed headers carrying its authserv-id.
        var result = message.Headers.FirstOrDefault(h => h.Field.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        var split = result.Split(';', 2);
        if (split.Length != 2 || !split[0].Trim().Equals(trustedService, StringComparison.OrdinalIgnoreCase)) return false;
        var domain = sender.Split('@').Last();
        return Regex.IsMatch(split[1], @"(?:^|;)\s*dmarc=pass\b[^;]*\bheader\.from=" +
            Regex.Escape(domain) + @"(?=\s|;|$)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }
    private static string HtmlText(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        foreach (var node in document.DocumentNode.Descendants().Where(n => n.Name is "script" or "style" or "head").ToList()) node.Remove();
        foreach (var node in document.DocumentNode.Descendants().Where(n => n.Name is "p" or "div" or "br" or "li").ToList())
            node.AppendChild(document.CreateTextNode("\n"));
        return WebUtility.HtmlDecode(document.DocumentNode.InnerText);
    }
    public static string Text(MimeMessage message)
    {
        var body = message.TextBody ?? (message.HtmlBody is { } html ? HtmlText(html) : "");
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var boundary = Array.FindIndex(lines, l => l.Trim().TrimStart('>').Trim() == EmailComposer.ReplyBoundary);
        return string.Join("\n", boundary >= 0 ? lines.Take(boundary) : lines).Trim();
    }
    public async Task<string> ProcessAsync(MimeMessage message, string source, bool localTrusted, CancellationToken ct = default)
    {
        var sourceKey = EmailComposer.Hash(options.Value.AccountKey + ":" + source);
        var messageKey = EmailComposer.Hash(options.Value.AccountKey + ":" + (message.MessageId ?? source));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.IncomingEmails.AnyAsync(m => m.SourceKey == sourceKey || m.MessageKey == messageKey, ct)) {
            await tx.CommitAsync(ct); return "Duplicate";
        }
        var senders = message.From.Mailboxes.ToList();
        var sender = senders.Count == 1 ? senders[0].Address : "";
        var body = Text(message);
        var row = new IncomingEmail {
            SourceKey = sourceKey, MessageKey = messageKey, Sender = sender[..Math.Min(sender.Length, 254)],
            Subject = (message.Subject ?? "")[..Math.Min(message.Subject?.Length ?? 0, 500)],
            Body = body[..Math.Min(body.Length, 12000)]
        };
        db.IncomingEmails.Add(row);
        async Task<string> Finish(string state, string reason) {
            row.State = state; row.Reason = reason;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return state;
        }
        if (message.Headers.Contains(HeaderId.AutoSubmitted) && message.Headers[HeaderId.AutoSubmitted] != "no" ||
            message.Headers.Contains(HeaderId.ListId) || message.Body is MultipartReport ||
            (message.Headers["Precedence"] ?? "").Equals("bulk", StringComparison.OrdinalIgnoreCase))
            return await Finish("Ignored", "Respuesta automática, lista o informe de entrega.");
        if (senders.Count != 1 || string.IsNullOrWhiteSpace(message.MessageId))
            return await Finish("Review", "Remitente ambiguo o correo sin Message-ID.");
        if (!localTrusted && !Authenticated(message, sender, options.Value.TrustedAuthenticationService))
            return await Finish("Review", "No se pudo validar DMARC mediante el servidor de correo de confianza.");
        var references = message.References.TakeLast(30).ToList();
        if (!string.IsNullOrEmpty(message.InReplyTo)) references.Add(message.InReplyTo);
        var related = await db.OutboundEmails.Where(m => m.TicketId != null && references.Contains(m.MessageId) &&
            (m.State == "Sent" || m.State == "Sending")).Select(m => new { m.TicketId, m.Recipient }).ToListAsync(ct);
        var ids = related.Select(m => m.TicketId!.Value).Distinct().ToList();
        if (ids.Count != 1) return await Finish("Review", "No hay una referencia única a un correo enviado por este sistema.");
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ids[0], ct);
        var (name, email) = await composer.ContactAsync(ticket);
        if (!email.Equals(sender, StringComparison.OrdinalIgnoreCase) ||
            !related.Any(r => r.Recipient.Equals(sender, StringComparison.OrdinalIgnoreCase)))
            return await Finish("Review", "El remitente no corresponde al solicitante y destinatario original.");
        row.TicketId = ticket.Id;
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
            return await Finish("Review", "Ticket cerrado o cancelado: requiere revisión del agente.");
        List<TicketAttachment> attachments;
        try { attachments = InboundAttachments.Read(message, ct); }
        catch (InvalidDataException ex) { return await Finish("Review", ex.Message); }
        if ((body.Length == 0 && attachments.Count == 0) || body.Length > 12000)
            return await Finish("Review", "El cuerpo está vacío o supera el límite de 12.000 caracteres.");
        if (body.Length == 0) body = "El cliente envió archivos adjuntos por correo.";
        foreach (var attachment in attachments) {
            attachment.TicketId = ticket.Id;
            db.TicketAttachments.Add(attachment);
        }
        ticket.Messages.Add(new TicketMessage { AuthorId = ticket.RequesterId, AuthorName = name,
            Body = body, Visibility = MessageVisibility.Public, Source = "Email" });
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.HasCustomerReply = true;
        if (ticket.Status is TicketStatus.WaitingRequester or TicketStatus.Resolved)
            ticket.Transition(TicketStatus.InProgress, "El cliente respondió por correo.", DateTime.UtcNow);
        ticket.Events.Add(new TicketEvent { ActorId = ticket.RequesterId, ActorName = name,
            Kind = "EmailReply", Detail = "Respuesta del cliente recibida por correo." });
        return await Finish("Accepted", "Respuesta incorporada al ticket.");
    }
}
