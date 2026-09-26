namespace Sidecil.Tickets.Domain;

public sealed class EmailTemplate
{
    public int Id { get; set; } = 1;
    public string Subject { get; set; } = "Recibimos tu solicitud: {asunto}";
    public string Body { get; set; } = "Hola {nombre},\n\nGracias por contactar a Sidecil. Registramos tu solicitud con el número {numero}. Nuestro equipo revisará tu caso y te acompañará hasta resolverlo.\n\nPuedes responder directamente a este correo para agregar información.\n\n{firma}";
    public string Signature { get; set; } = "Equipo de atención\nSidecil";
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class OutboundEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long? TicketId { get; set; }
    public string DeduplicationKey { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Kind { get; set; } = "Receipt";
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LastError { get; set; }
}

public sealed class IncomingEmail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceKey { get; set; } = "";
    public string MessageKey { get; set; } = "";
    public long? TicketId { get; set; }
    public string Sender { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string State { get; set; } = "Review";
    public string Reason { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public sealed class GuestSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "General";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public long? TicketId { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class MailboxCursor
{
    public string Id { get; set; } = "";
    public long UidValidity { get; set; }
    public long LastUid { get; set; }
    public byte[] Version { get; set; } = [];
}
