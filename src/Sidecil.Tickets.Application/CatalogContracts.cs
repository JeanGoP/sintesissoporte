using System.ComponentModel.DataAnnotations;
namespace Sidecil.Tickets.Application;

public record CategoryRequest([property: Required, StringLength(60, MinimumLength = 2)] string Name, bool Enabled = true);
public record ModuleRequest([property: Required, StringLength(120, MinimumLength = 2)] string Name, Guid CategoryId, bool Enabled = true);
public record UserScopeRequest(Guid[] ModuleIds, Guid[] OrganizationIds);
public record ClassificationRequest(Guid ModuleId, Guid? OrganizationId, string? CompanyName);
