namespace Sidecil.Tickets.Domain;

public sealed class SupportCategory { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = ""; public bool Enabled { get; set; } = true; }
public sealed class SupportModule { public Guid Id { get; set; } = Guid.NewGuid(); public Guid CategoryId { get; set; } public string Name { get; set; } = ""; public bool Enabled { get; set; } = true; }
public sealed class AgentModule { public string UserId { get; set; } = ""; public Guid ModuleId { get; set; } }
public sealed class UserOrganization { public string UserId { get; set; } = ""; public Guid OrganizationId { get; set; } }
