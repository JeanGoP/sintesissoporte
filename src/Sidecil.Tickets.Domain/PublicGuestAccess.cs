namespace Sidecil.Tickets.Domain;
public sealed class PublicGuestAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
