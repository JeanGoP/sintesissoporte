namespace Sidecil.Tickets.Domain;
public sealed class GuestAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuestSubmissionId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int Length { get; set; }
    public byte[] Content { get; set; } = [];
}
