namespace Sidecil.Tickets.Domain;

public enum TicketStatus { New, InProgress, WaitingRequester, WaitingThirdParty, Resolved, Closed, Cancelled }
public enum TicketPriority { Low, Normal, High, Urgent }
public enum MessageVisibility { Public, Internal }

public sealed class Ticket
{
    public long Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = "";
    public string Category { get; set; } = "General";
    public Guid OrganizationId { get; set; }
    public Guid TeamId { get; set; }
    public string? RequesterId { get; set; }
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public bool HasCustomerReply { get; set; }
    public string? AssigneeId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public byte[] Version { get; set; } = [];
    public List<TicketMessage> Messages { get; set; } = [];
    public List<TicketEvent> Events { get; set; } = [];

    public static IReadOnlyList<TicketStatus> NextStatuses(TicketStatus status) => status switch
    {
        TicketStatus.New => [TicketStatus.InProgress, TicketStatus.Cancelled],
        TicketStatus.InProgress => [TicketStatus.WaitingRequester, TicketStatus.WaitingThirdParty, TicketStatus.Resolved, TicketStatus.Cancelled],
        TicketStatus.WaitingRequester or TicketStatus.WaitingThirdParty => [TicketStatus.InProgress, TicketStatus.Cancelled],
        TicketStatus.Resolved => [TicketStatus.Closed, TicketStatus.InProgress],
        TicketStatus.Closed => [TicketStatus.InProgress],
        TicketStatus.Cancelled => [],
        _ => []
    };

    public void Transition(TicketStatus next, string reason, DateTime now)
    {
        if (!NextStatuses(Status).Contains(next)) throw new InvalidOperationException("Esta transición no está permitida.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Escribe el motivo o la solución del cambio.");
        if (next == TicketStatus.InProgress && Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            DueAt = now.AddHours(TargetHours(Priority));
            ResolvedAt = null;
        }
        if (next == TicketStatus.Resolved) ResolvedAt = now;
        Status = next;
        UpdatedAt = now;
    }

    // Primera entrega: objetivo operativo de resolución en horas corridas, no calendario SLA contractual.
    public static int TargetHours(TicketPriority priority) => priority switch
    {
        TicketPriority.Urgent => 4, TicketPriority.High => 8, TicketPriority.Normal => 24, _ => 72
    };
}

public sealed class TicketMessage
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string Source { get; set; } = "Portal";
    public string Body { get; set; } = "";
    public MessageVisibility Visibility { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class TicketEvent
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}

public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}
