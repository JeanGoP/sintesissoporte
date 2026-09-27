using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;

internal static class MailConnectionCheck
{
    public static async Task<int> CheckImapAsync(MailOptions mail)
    {
        var step = "conexion TLS a IMAP";
        try {
            using var imap = new ImapClient { Timeout = 30000 };
            await imap.ConnectAsync(mail.ImapHost, mail.ImapPort, SecureSocketOptions.SslOnConnect);
            Console.WriteLine("IMAP: conexion TLS correcta.");
            step = "autenticacion IMAP";
            if (!string.IsNullOrEmpty(mail.ImapAccessToken)) await imap.AuthenticateAsync(new SaslMechanismOAuth2(mail.ImapUser, mail.ImapAccessToken));
            else await imap.AuthenticateAsync(mail.ImapUser, mail.ImapPassword);
            Console.WriteLine("IMAP: autenticacion correcta.");
            step = "apertura de carpeta IMAP";
            var folder = await imap.GetFolderAsync(mail.ImapFolder);
            await folder.OpenAsync(FolderAccess.ReadOnly);
            await imap.DisconnectAsync(true);
            Console.WriteLine("IMAP: carpeta accesible. No se enviaron ni importaron mensajes.");
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine($"Fallo en {step}: {ex.GetType().FullName}.");
            // Clasifica respuestas conocidas sin imprimir mensajes del proveedor ni credenciales.
            if (ex is MailKit.Security.AuthenticationException) {
                var response = ex.Message;
                if (response.Contains("Application-specific password", StringComparison.OrdinalIgnoreCase))
                    Console.Error.WriteLine("GMAIL_APP_PASSWORD_REQUIRED: Google exige una contrasena de aplicacion.");
                else if (response.Contains("Web login required", StringComparison.OrdinalIgnoreCase) || response.Contains("Please log in", StringComparison.OrdinalIgnoreCase))
                    Console.Error.WriteLine("GMAIL_WEB_LOGIN_REQUIRED: Google pide revisar el acceso desde el navegador de esa cuenta.");
                else if (response.Contains("Invalid credentials", StringComparison.OrdinalIgnoreCase) || response.Contains("AUTHENTICATIONFAILED", StringComparison.OrdinalIgnoreCase))
                    Console.Error.WriteLine("GMAIL_CREDENTIALS_REJECTED: El proveedor rechazo la combinacion de cuenta y credencial; comprueba a que cuenta pertenece la contrasena de aplicacion.");
                else Console.Error.WriteLine("IMAP_AUTH_REJECTED: El proveedor rechazo la autenticacion. Revisa la seguridad y restricciones de la cuenta.");
            } else if (ex is SslHandshakeException || ex is System.Security.Authentication.AuthenticationException) {
                Console.Error.WriteLine("IMAP_TLS_FAILED: Revisa el certificado, la fecha del servidor y posibles inspecciones TLS. No desactives la validacion del certificado.");
            }
            return 1;
        }
    }
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
            if (!string.IsNullOrEmpty(mail.ImapHost)) return await CheckImapAsync(mail);
            return 0;
        } catch (Exception ex) {
            Console.Error.WriteLine($"Fallo en {step}: {ex.GetType().Name}. Revisa la configuracion y el acceso de red del servidor.");
            return 1;
        }
    }
}