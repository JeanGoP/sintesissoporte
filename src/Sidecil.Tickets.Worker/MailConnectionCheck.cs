using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

internal static class MailConnectionCheck
{
    public static async Task<int> RunAsync(string connection, MailOptions mail)
    {
        var step = "configuracion";
        try {
            if (mail.Mode != "Smtp") throw new InvalidOperationException();
            MimeKit.MailboxAddress.Parse(mail.FromAddress);
            if (!string.IsNullOrWhiteSpace(mail.ReplyToAddress)) MimeKit.MailboxAddress.Parse(mail.ReplyToAddress);
            step = "SQL Server y tablas de correo";
            await using var db = new TicketsDbContext(new DbContextOptionsBuilder<TicketsDbContext>().UseSqlServer(connection).Options);
            await db.OutboundEmails.AsNoTracking().Take(1).Select(x => x.Id).ToListAsync();
            await db.IncomingEmails.AsNoTracking().Take(1).Select(x => x.Id).ToListAsync();
            await db.MailboxCursors.AsNoTracking().Take(1).Select(x => x.Id).ToListAsync();
            Console.WriteLine("SQL Server y tablas de correo: correctos.");
            step = "envio SMTP";
            using var smtp = new SmtpClient { Timeout = 30000 };
            await smtp.ConnectAsync(mail.SmtpHost, mail.SmtpPort, mail.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            if (!string.IsNullOrEmpty(mail.SmtpAccessToken)) await smtp.AuthenticateAsync(new SaslMechanismOAuth2(mail.SmtpUser, mail.SmtpAccessToken));
            else await smtp.AuthenticateAsync(mail.SmtpUser, mail.SmtpPassword);
            await smtp.DisconnectAsync(true);
            Console.WriteLine("SMTP: autenticacion correcta (no se enviaron mensajes).");
            if (!string.IsNullOrEmpty(mail.ImapHost)) {
                step = "recepcion IMAP";
                using var imap = new ImapClient { Timeout = 30000 };
                await imap.ConnectAsync(mail.ImapHost, mail.ImapPort, SecureSocketOptions.SslOnConnect);
                if (!string.IsNullOrEmpty(mail.ImapAccessToken)) await imap.AuthenticateAsync(new SaslMechanismOAuth2(mail.ImapUser, mail.ImapAccessToken));
                else await imap.AuthenticateAsync(mail.ImapUser, mail.ImapPassword);
                var folder = await imap.GetFolderAsync(mail.ImapFolder);
                await folder.OpenAsync(FolderAccess.ReadOnly);
                await imap.DisconnectAsync(true);
                Console.WriteLine("IMAP: autenticacion correcta (no se importaron mensajes).");
            }
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine($"Fallo en {step}: {ex.GetType().Name}. Revisa la configuracion y el acceso de red del servidor.");
            return 1;
        }
    }
}