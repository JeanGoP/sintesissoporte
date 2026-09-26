namespace Sidecil.Tickets.Infrastructure.Mail;
public sealed class MailOptions
{
    public string Mode { get; set; } = "Disabled"; // Disabled, Pickup (Development), Smtp
    public string FromAddress { get; set; } = "soporte@sidecil.invalid";
    public string FromName { get; set; } = "Sidecil · Atención al cliente";
    public string PublicBaseUrl { get; set; } = "http://localhost:5080";
    public string PickupDirectory { get; set; } = "";
    public string InboxDirectory { get; set; } = "";
    public string AccountKey { get; set; } = "sidecil-support";
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string SmtpAccessToken { get; set; } = "";
    public string ImapHost { get; set; } = "";
    public int ImapPort { get; set; } = 993;
    public string ImapUser { get; set; } = "";
    public string ImapPassword { get; set; } = "";
    public string ImapAccessToken { get; set; } = "";
    public string ImapFolder { get; set; } = "INBOX";
    public string TrustedAuthenticationService { get; set; } = "";
}
