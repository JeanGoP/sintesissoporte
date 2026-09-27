using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit;
using Sidecil.Tickets.Infrastructure;
using Sidecil.Tickets.Infrastructure.Mail;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed partial class PortalOidcTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task EmailAttachmentsAreAtomicAndDeduplicated(bool withBody, bool invalid)
    {
        using var factory = new PortalOidcFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var email = Guid.NewGuid() + "@test.invalid";
        var ticket = new Ticket { Subject = "Adjuntos por correo", GuestEmail = email, GuestName = "Cliente",
            OrganizationId = TicketsDbContext.GuestOrganizationId, TeamId = await db.Teams.Select(x => x.Id).FirstAsync(),
            Status = TicketStatus.WaitingRequester };
        db.Tickets.Add(ticket); await db.SaveChangesAsync();
        var composer = new EmailComposer(db);
        var sent = composer.Queue(email, "Ticket", "Texto", Guid.NewGuid().ToString(), "Receipt", ticket.Id);
        sent.State = "Sent"; await db.SaveChangesAsync();
        using var message = new MimeMessage { MessageId = Guid.NewGuid() + "@test.invalid", InReplyTo = sent.MessageId };
        message.From.Add(MailboxAddress.Parse(email));
        var builder = new BodyBuilder { TextBody = withBody ? "Más evidencia" : "" };
        builder.Attachments.Add("evidencia.txt", "Información adicional"u8.ToArray());
        if (invalid) builder.Attachments.Add("programa.exe", new byte[] { 1, 2, 3 });
        message.Body = builder.ToMessageBody();
        var processor = new InboundProcessor(db, composer, Options.Create(new MailOptions()));
        try {
            Assert.Equal(invalid ? "Review" : "Accepted", await processor.ProcessAsync(message, message.MessageId, true));
            Assert.Equal("Duplicate", await processor.ProcessAsync(message, message.MessageId, true));
            Assert.Equal(invalid ? 0 : 1, await db.TicketAttachments.CountAsync(x => x.TicketId == ticket.Id));
            Assert.Equal(invalid ? 0 : 1, ticket.Messages.Count);
            if (!invalid) {
                Assert.Equal(TicketStatus.InProgress, ticket.Status);
                Assert.True(ticket.HasCustomerReply);
                Assert.Equal("Información adicional"u8.ToArray(), (await db.TicketAttachments.SingleAsync(x => x.TicketId == ticket.Id)).Content);
            }
        } finally {
            await db.IncomingEmails.Where(x => x.TicketId == ticket.Id).ExecuteDeleteAsync();
            await db.OutboundEmails.Where(x => x.TicketId == ticket.Id).ExecuteDeleteAsync();
            await db.Tickets.Where(x => x.Id == ticket.Id).ExecuteDeleteAsync();
        }
    }
}
public sealed class InboundAttachmentTests
{
    [Theory]
    [InlineData(4, 10)]
    [InlineData(1, 5242881)]
    public void RejectsTooManyOrOversizedFiles(int count, int size)
    {
        using var message = new MimeMessage();
        var builder = new BodyBuilder { TextBody = "Evidencia" };
        for (var i = 0; i < count; i++) builder.Attachments.Add("archivo.txt", Enumerable.Repeat((byte)65, size).ToArray());
        message.Body = builder.ToMessageBody();
        Assert.Throws<InvalidDataException>(() => InboundAttachments.Read(message));
    }
    [Fact]
    public void ImportsInlineImagesAndNormalizesNames()
    {
        using var message = new MimeMessage();
        var builder = new BodyBuilder { HtmlBody = "<p>Captura</p><img src='cid:captura'>" };
        var part = builder.LinkedResources.Add("captura.png", new byte[] {137,80,78,71,13,10,26,10});
        part.ContentId = "captura";
        message.Body = builder.ToMessageBody();
        var file = Assert.Single(InboundAttachments.Read(message));
        Assert.Equal("image/png", file.ContentType);
    }
}
