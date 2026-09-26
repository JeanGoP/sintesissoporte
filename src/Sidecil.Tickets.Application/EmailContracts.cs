using System.ComponentModel.DataAnnotations;
namespace Sidecil.Tickets.Application;
public record GuestTicketRequest(
    [property: Required, StringLength(120, MinimumLength = 2)] string Name,
    [property: Required, EmailAddress, StringLength(200)] string Email,
    [property: Required, StringLength(180, MinimumLength = 5)] string Subject,
    [property: Required, StringLength(12000, MinimumLength = 10)] string Body,
    [property: Required, StringLength(60)] string Category);
public record ConfirmGuestRequest([property: Required, StringLength(100, MinimumLength = 40)] string Token);
public record EmailTemplateRequest(
    [property: Required, StringLength(200, MinimumLength = 3)] string Subject,
    [property: Required, StringLength(8000, MinimumLength = 20)] string Body,
    [property: Required, StringLength(500)] string Signature,
    [property: Required] string Version);
