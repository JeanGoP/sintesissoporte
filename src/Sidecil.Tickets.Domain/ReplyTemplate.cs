namespace Sidecil.Tickets.Domain;

public sealed class ReplyTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
