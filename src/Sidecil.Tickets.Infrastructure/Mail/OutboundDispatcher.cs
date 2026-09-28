using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Sidecil.Tickets.Infrastructure.Mail;
public sealed class OutboundDispatcher(TicketsDbContext db, IOptions<MailOptions> options)
{
    public static MimeMessage CreateMessage(Sidecil.Tickets.Domain.OutboundEmail mail, MailOptions o)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(MailboxAddress.Parse(mail.Recipient));
        message.ReplyTo.Add(MailboxAddress.Parse(string.IsNullOrWhiteSpace(o.ReplyToAddress) ? o.FromAddress : o.ReplyToAddress));
        message.Subject = mail.Subject;
        message.MessageId = mail.MessageId;
        message.Date = new DateTimeOffset(DateTime.SpecifyKind(mail.CreatedAt, DateTimeKind.Utc));
        message.Headers.Add(HeaderId.AutoSubmitted, "auto-generated");
        message.Headers.Add("X-Auto-Response-Suppress", "All");
        message.Body = new TextPart("plain") { Text = mail.Body };
        return message;
    }
    public Task<int> PurgeExpiredGuestAttachmentsAsync(CancellationToken ct = default) =>
        db.GuestAttachments.Where(f => db.GuestSubmissions.Any(s => s.Id == f.GuestSubmissionId && s.TicketId == null && s.ExpiresAt < DateTime.UtcNow)).ExecuteDeleteAsync(ct);

    public async Task<int> DispatchAsync(CancellationToken ct = default)
    {
        var o = options.Value;
        if (o.Mode == "Disabled") return 0;
        var now = DateTime.UtcNow;
        await PurgeExpiredGuestAttachmentsAsync(ct);
        await db.PublicGuestAccesses.Where(x => x.ExpiresAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        var ids = await db.OutboundEmails.AsNoTracking()
            .Where(m => ((m.State == "Pending" && m.NextAttemptAt <= now) || (m.State == "Sending" && m.LeaseUntil < now)) && m.Attempts < 5)
            .OrderBy(m => m.CreatedAt).Select(m => m.Id).Take(20).ToListAsync(ct);
        foreach (var id in ids) {
            var lease = Guid.NewGuid();
            var claimed = await db.OutboundEmails.Where(m => m.Id == id && m.Attempts < 5 &&
                ((m.State == "Pending" && m.NextAttemptAt <= now) || (m.State == "Sending" && m.LeaseUntil < now)))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, "Sending").SetProperty(m => m.LeaseId, lease)
                    .SetProperty(m => m.LeaseUntil, DateTime.UtcNow.AddMinutes(3)).SetProperty(m => m.Attempts, m => m.Attempts + 1), ct);
            if (claimed == 0) continue;
            var mail = await db.OutboundEmails.AsNoTracking().SingleAsync(m => m.Id == id, ct);
            if (mail.ExpiresAt is { } expires && expires <= DateTime.UtcNow) {
                await db.OutboundEmails.Where(m => m.Id == id && m.LeaseId == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, "Expired").SetProperty(m => m.LeaseUntil, (DateTime?)null), ct);
                continue;
            }
            try {
                using var message = CreateMessage(mail, o);
                if (o.Mode == "Pickup") {
                    Directory.CreateDirectory(o.PickupDirectory);
                    var destination = Path.Combine(o.PickupDirectory, mail.Id + ".eml");
                    if (!File.Exists(destination)) {
                        var temp = destination + "." + lease + ".tmp";
                        await message.WriteToAsync(temp, ct);
                        File.Move(temp, destination, overwrite: true);
                    }
                } else {
                    using var client = new SmtpClient { Timeout = 30000 };
                    await client.ConnectAsync(o.SmtpHost, o.SmtpPort, o.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
                    if (!string.IsNullOrEmpty(o.SmtpAccessToken)) await client.AuthenticateAsync(new SaslMechanismOAuth2(o.SmtpUser, o.SmtpAccessToken), ct);
                    else await client.AuthenticateAsync(o.SmtpUser, o.SmtpPassword, ct);
                    await client.SendAsync(message, ct);
                    await client.DisconnectAsync(true, ct);
                }
                await db.OutboundEmails.Where(m => m.Id == id && m.LeaseId == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, "Sent").SetProperty(m => m.SentAt, DateTime.UtcNow)
                        .SetProperty(m => m.LeaseUntil, (DateTime?)null).SetProperty(m => m.LastError, (string?)null), ct);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                await db.OutboundEmails.Where(m => m.Id == id && m.LeaseId == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, mail.Attempts >= 5 ? "Failed" : "Pending")
                        .SetProperty(m => m.NextAttemptAt, DateTime.UtcNow.AddSeconds(Math.Pow(2, mail.Attempts) * 30 + Random.Shared.Next(10)))
                        .SetProperty(m => m.LastError, ex.GetType().Name).SetProperty(m => m.LeaseUntil, (DateTime?)null), ct);
            }
        }
        // A crash during the final attempt must not leave an invisible, permanently leased job.
        await db.OutboundEmails.Where(m => m.State == "Sending" && m.LeaseUntil < now && m.Attempts >= 5)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, "Failed").SetProperty(m => m.LastError, "LeaseExpired"), ct);
        return ids.Count;
    }
}
