namespace Sidecil.Tickets.Domain;

public sealed class ChatSite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Origin { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ChatConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SiteId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Module { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "General";
    public Guid? ModuleId { get; set; }
    public string? CompanyName { get; set; }
    public string? IdentityProvider { get; set; }
    public string? IdentityIssuer { get; set; }
    public string? IdentitySubject { get; set; }
    public DateTime? IdentityVerifiedAt { get; set; }
    public string? VerificationHash { get; set; }
    public DateTime? VerificationExpiresAt { get; set; }
    public DateTime? VerificationSentAt { get; set; }
    public int VerificationAttempts { get; set; }
    public Guid? GuestSubmissionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public byte[] Version { get; set; } = [];
}

public sealed class ChatAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public int Length { get; set; }
    public byte[] Content { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ChatLoginAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Provider { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class PortalLoginAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string? LinkUserId { get; set; }
    public string? SecurityStamp { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
