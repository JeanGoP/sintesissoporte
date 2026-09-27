using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using Sidecil.Tickets.Domain;

namespace Sidecil.Tickets.Infrastructure.Mail;
public sealed class InboxPoller(TicketsDbContext db, InboundProcessor processor, IOptions<MailOptions> options)
{
    public async Task PollAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (o.Mode == "Disabled") return;
        if (o.Mode == "Pickup") {
            Directory.CreateDirectory(o.InboxDirectory);
            var archive = Path.Combine(o.InboxDirectory, "processed");
            Directory.CreateDirectory(archive);
            foreach (var file in Directory.EnumerateFiles(o.InboxDirectory, "*.eml").Take(50)) {
                if (new FileInfo(file).Length > InboundAttachments.MaxMessageBytes) {
                    await QuarantineAsync("file:" + Path.GetFileName(file), "Correo de prueba mayor de 24 MB.", ct);
                } else {
                    try {
                        using var message = await MimeMessage.LoadAsync(file, ct);
                        await processor.ProcessAsync(message, "file:" + Path.GetFileName(file), localTrusted: true, ct);
                    } catch (FormatException) {
                        await QuarantineAsync("file:" + Path.GetFileName(file), "El formato MIME no es válido.", ct);
                    }
                }
                File.Move(file, Path.Combine(archive, Path.GetFileName(file)), overwrite: true);
                db.ChangeTracker.Clear();
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(o.ImapHost)) return;
        using var client = new ImapClient { Timeout = 30000 };
        await client.ConnectAsync(o.ImapHost, o.ImapPort, SecureSocketOptions.SslOnConnect, ct);
        if (!string.IsNullOrEmpty(o.ImapAccessToken)) await client.AuthenticateAsync(new SaslMechanismOAuth2(o.ImapUser, o.ImapAccessToken), ct);
        else await client.AuthenticateAsync(o.ImapUser, o.ImapPassword, ct);
        var folder = await client.GetFolderAsync(o.ImapFolder, ct);
        await folder.OpenAsync(FolderAccess.ReadOnly, ct);
        var key = EmailComposer.Hash(o.AccountKey + ":" + o.ImapFolder);
        var cursor = await db.MailboxCursors.FindAsync([key], ct);
        if (cursor is null) {
            cursor = new MailboxCursor { Id = key, UidValidity = folder.UidValidity };
            db.MailboxCursors.Add(cursor); await db.SaveChangesAsync(ct);
        }
        if (cursor.UidValidity != folder.UidValidity) {
            cursor.UidValidity = folder.UidValidity; cursor.LastUid = 0;
            await db.SaveChangesAsync(ct);
        }
        var first = new UniqueId((uint)Math.Min(cursor.LastUid + 1, uint.MaxValue));
        var all = await folder.SearchAsync(SearchQuery.Uids(new UniqueIdRange(first, UniqueId.MaxValue)), ct);
        var ids = all.Where(u => u.Id > cursor.LastUid).OrderBy(u => u.Id).Take(50).ToList();
        if (ids.Count > 0) {
            var summaries = await folder.FetchAsync(ids, MessageSummaryItems.UniqueId | MessageSummaryItems.Size, ct);
            foreach (var item in summaries.OrderBy(x => x.UniqueId.Id)) {
                var source = $"imap:{o.ImapFolder}:{folder.UidValidity}:{item.UniqueId.Id}";
                if (item.Size > InboundAttachments.MaxMessageBytes) await QuarantineAsync(source, "Correo mayor de 24 MB; revisar en el buzón.", ct);
                else {
                    try {
                        using var message = await folder.GetMessageAsync(item.UniqueId, ct);
                        await processor.ProcessAsync(message, source, localTrusted: false, ct);
                    } catch (FormatException) {
                        await QuarantineAsync(source, "El formato MIME no es válido; revisar en el buzón.", ct);
                    }
                }
                cursor.LastUid = item.UniqueId.Id;
                await db.SaveChangesAsync(ct);
            }
        }
        await client.DisconnectAsync(true, ct);
    }
    private async Task QuarantineAsync(string source, string reason, CancellationToken ct)
    {
        var key = EmailComposer.Hash(options.Value.AccountKey + ":" + source);
        if (await db.IncomingEmails.AnyAsync(x => x.SourceKey == key, ct)) return;
        db.IncomingEmails.Add(new IncomingEmail { SourceKey = key, MessageKey = key, State = "Review", Reason = reason });
        await db.SaveChangesAsync(ct);
    }
}
