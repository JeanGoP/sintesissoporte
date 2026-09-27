namespace Sidecil.Tickets.Domain;
public sealed class TicketAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long TicketId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int Length { get; set; }
    public byte[] Content { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
