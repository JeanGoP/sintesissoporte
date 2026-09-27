using System.ComponentModel.DataAnnotations;
using Sidecil.Tickets.Domain;

namespace Sidecil.Tickets.Application;

public record CreateTicketRequest(
    [property: Required, StringLength(180, MinimumLength = 5)] string Subject,
    [property: Required, StringLength(12000, MinimumLength = 10)] string Body,
    TicketPriority Priority,
    [property: Required, StringLength(60)] string Category, Guid? ModuleId = null, string? CompanyName = null, Guid? OrganizationId = null);
public record AddMessageRequest([property: Required, StringLength(12000, MinimumLength = 1)] string Body, MessageVisibility Visibility);
public record TransitionRequest(TicketStatus Status, [property: Required, StringLength(2000, MinimumLength = 3)] string Reason);
public record AssignmentRequest(string? AssigneeId);
public record LoginRequest([property: Required, EmailAddress] string Email, [property: Required] string Password);
public record ChangePasswordRequest([property: Required] string CurrentPassword, [property: Required, MinLength(12)] string NewPassword);
public record CreateUserRequest(
    [property: Required, StringLength(120)] string DisplayName,
    [property: Required, EmailAddress, StringLength(200)] string Email,
    Guid OrganizationId,
    Guid? TeamId,
    [property: Required] string Role, Guid[]? ModuleIds = null, Guid[]? OrganizationIds = null);
public record CreateOrganizationRequest([property: Required, StringLength(120, MinimumLength = 2)] string Name);

public static class AccessRules
{
    public static bool CanAccess(string role, string userId, Guid organizationId, Guid? teamId, Ticket ticket, IReadOnlyCollection<Guid>? moduleIds = null) =>
        role == "Admin" ||
        role == "Agent" && ticket.ModuleId.HasValue && moduleIds != null && moduleIds.Contains(ticket.ModuleId.Value) ||
        role == "Requester" && ticket.RequesterId == userId;
    public static bool IsStaff(string role) => role is "Agent" or "Admin";
    public static bool CanReadMessage(string role, MessageVisibility visibility) => IsStaff(role) || visibility == MessageVisibility.Public;
}
