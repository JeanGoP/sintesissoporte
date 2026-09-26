using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sidecil.Tickets.Domain;

namespace Sidecil.Tickets.Infrastructure.Mail;
public sealed class EmailComposer(TicketsDbContext db)
{
    public const string ReplyBoundary = "--- Responde encima de esta línea ---";
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Number(long id) => $"SC-{id:00000}";
    private static readonly string[] Variables = ["nombre", "numero", "asunto", "firma"];
    public static bool ValidTemplate(string value) => Regex.Matches(value, @"\{([^{}]+)\}").All(m => Variables.Contains(m.Groups[1].Value));
    // One pass: user content that resembles a template variable is never interpreted.
    public static string Render(string template, string name, string number, string subject, string signature) =>
        Regex.Replace(template, @"\{(nombre|numero|asunto|firma)\}", m => m.Groups[1].Value switch {
            "nombre" => name, "numero" => number, "asunto" => subject, _ => signature
        });
    public OutboundEmail Queue(string recipient, string subject, string body, string key, string kind, long? ticketId = null, DateTime? expires = null)
    {
        var mail = new OutboundEmail {
            Recipient = recipient, Subject = subject.Replace("\r", " ").Replace("\n", " "),
            Body = body, DeduplicationKey = key, Kind = kind, TicketId = ticketId,
            MessageId = Guid.NewGuid().ToString("N") + "@sidecil-tickets.invalid", ExpiresAt = expires
        };
        db.OutboundEmails.Add(mail);
        return mail;
    }
    public async Task ReceiptAsync(Ticket ticket)
    {
        var template = await db.EmailTemplates.AsNoTracking().SingleAsync(x => x.Id == 1);
        var (name, email) = await ContactAsync(ticket);
        var number = Number(ticket.Id);
        Queue(email, $"[{number}] " + Render(template.Subject, name, number, ticket.Subject, template.Signature),
            ReplyBoundary + "\n\n" + Render(template.Body, name, number, ticket.Subject, template.Signature),
            "receipt:" + ticket.PublicId, "Receipt", ticket.Id);
    }
    public async Task AgentReplyAsync(Ticket ticket, TicketMessage message)
    {
        if (message.Visibility != MessageVisibility.Public) throw new InvalidOperationException("Las notas internas no se envían por correo.");
        var template = await db.EmailTemplates.AsNoTracking().SingleAsync(x => x.Id == 1);
        var (name, email) = await ContactAsync(ticket);
        Queue(email, $"[{Number(ticket.Id)}] Respuesta a tu solicitud: {ticket.Subject}",
            $"{ReplyBoundary}\n\nHola {name},\n\n{message.Body}\n\n{template.Signature}",
            "reply:" + message.Id, "Reply", ticket.Id);
    }
    public async Task<(string Name, string Email)> ContactAsync(Ticket ticket)
    {
        if (ticket.RequesterId is null) return (ticket.GuestName!, ticket.GuestEmail!);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == ticket.RequesterId);
        return (user.DisplayName, user.Email!);
    }
}
