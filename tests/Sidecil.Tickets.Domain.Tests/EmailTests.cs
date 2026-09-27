using MimeKit;
using Sidecil.Tickets.Infrastructure.Mail;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed class EmailTests
{
    [Theory]
    [InlineData("responses@example.test", "responses@example.test")]
    [InlineData("", "support@example.test")]
    public void OutgoingMimePreservesThreadIdAndRoutesRepliesSeparately(string replyTo, string expected)
    {
        var options = new MailOptions { FromAddress = "support@example.test", FromName = "Support", ReplyToAddress = replyTo };
        var mail = new Sidecil.Tickets.Domain.OutboundEmail {
            Recipient = "customer@example.test", Subject = "[SC-00001] Consulta", Body = "Respuesta",
            MessageId = "stable-thread@example.test", CreatedAt = DateTime.UtcNow
        };
        using var message = OutboundDispatcher.CreateMessage(mail, options);
        using var buffer = new MemoryStream();
        message.WriteTo(buffer); buffer.Position = 0;
        using var parsed = MimeMessage.Load(buffer);
        Assert.Equal("support@example.test", parsed.From.Mailboxes.Single().Address);
        Assert.Equal(expected, parsed.ReplyTo.Mailboxes.Single().Address);
        Assert.Equal(mail.MessageId, parsed.MessageId);
        Assert.Equal(mail.Body, parsed.TextBody?.TrimEnd('\r', '\n'));
    }
    [Fact] public void TemplateExpansionIsNotRecursive()
    {
        Assert.Equal("Hola {firma}, SC-00012 / Soporte", EmailComposer.Render("Hola {nombre}, {numero} / {firma}", "{firma}", "SC-00012", "Consulta", "Soporte"));
    }
    [Theory]
    [InlineData("{nombre} {numero} {asunto} {firma}", true)]
    [InlineData("Hola {password}", false)]
    public void OnlyKnownVariablesAreAccepted(string template, bool valid) => Assert.Equal(valid, EmailComposer.ValidTemplate(template));
    [Fact] public void HtmlEmailBecomesPlainTextWithoutActiveContent()
    {
        var message = new MimeMessage { Body = new TextPart("html") { Text = "<html><head><style>hidden</style></head><body><p>Hola &amp; gracias</p><script>alert('x')</script><p>Necesito ayuda</p></body></html>" } };
        var text = InboundProcessor.Text(message);
        Assert.Contains("Hola & gracias", text);
        Assert.Contains("Necesito ayuda", text);
        Assert.DoesNotContain("alert", text);
        Assert.DoesNotContain("hidden", text);
    }
    [Fact] public void QuotedSystemBoundaryDoesNotRepeatOriginalMail()
    {
        var message = new MimeMessage { Body = new TextPart("plain") { Text = "Mi respuesta\r\n\r\n> " + EmailComposer.ReplyBoundary + "\r\n> Texto citado" } };
        Assert.Equal("Mi respuesta", InboundProcessor.Text(message));
    }
    [Theory]
    [InlineData("mx.provider.test; dmarc=pass header.from=client.test", true)]
    [InlineData("mx.provider.test; dmarc=fail header.from=client.test", false)]
    [InlineData("attacker.test; dmarc=pass header.from=client.test", false)]
    [InlineData("mx.provider.test; dmarc=pass header.from=other.test", false)]
    public void IncomingAuthenticationRequiresTrustedProviderAndAlignedDomain(string header, bool valid)
    {
        var message = new MimeMessage(); message.Headers.Add("Authentication-Results", header);
        Assert.Equal(valid, InboundProcessor.Authenticated(message, "person@client.test", "mx.provider.test"));
    }
    [Fact] public void AdditionalSpoofedAuthenticationHeaderCannotOverrideFirst()
    {
        var message = new MimeMessage();
        message.Headers.Add("Authentication-Results", "mx.provider.test; dmarc=fail header.from=client.test");
        message.Headers.Add("Authentication-Results", "mx.provider.test; dmarc=pass header.from=client.test");
        Assert.False(InboundProcessor.Authenticated(message, "person@client.test", "mx.provider.test"));
    }
}
