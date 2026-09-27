using Sidecil.Tickets.Application;
using Sidecil.Tickets.Domain;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed class TicketTests {
    [Fact] public void RequesterCannotReadAnotherPersonsTicketInSameOrganization() {
        var organization = Guid.NewGuid();
        var ticket = new Ticket { OrganizationId = organization, RequesterId = "alice" };
        Assert.False(AccessRules.CanAccess("Requester", "bob", organization, null, ticket));
        Assert.True(AccessRules.CanAccess("Requester", "alice", organization, null, ticket));
    }
    [Fact] public void AgentNeedsExplicitModulePermission() {
        var ticket = new Ticket { TeamId = Guid.NewGuid(), ModuleId = Guid.NewGuid() };
        Assert.False(AccessRules.CanAccess("Agent", "agent", Guid.NewGuid(), Guid.NewGuid(), ticket));
        Assert.True(AccessRules.CanAccess("Agent", "agent", Guid.NewGuid(), ticket.TeamId, ticket, new[] { ticket.ModuleId!.Value }));
    }
    [Theory] [InlineData("Requester", false)] [InlineData("Agent", true)] [InlineData("Admin", true)]
    public void InternalMessagesRequireStaff(string role, bool expected) =>
        Assert.Equal(expected, AccessRules.CanReadMessage(role, MessageVisibility.Internal));
    [Fact] public void CannotCloseNewTicketWithoutResolution() =>
        Assert.Throws<InvalidOperationException>(() => new Ticket().Transition(TicketStatus.Closed, "cerrar", DateTime.UtcNow));
    [Fact] public void ResolutionRequiresReason() =>
        Assert.Throws<InvalidOperationException>(() => new Ticket { Status = TicketStatus.InProgress }.Transition(TicketStatus.Resolved, " ", DateTime.UtcNow));
    [Fact] public void ReopeningStartsNewResolutionTarget() {
        var now = DateTime.UtcNow;
        var ticket = new Ticket { Status = TicketStatus.Resolved, Priority = TicketPriority.High, ResolvedAt = now.AddDays(-1) };
        ticket.Transition(TicketStatus.InProgress, "El problema persiste", now);
        Assert.Null(ticket.ResolvedAt);
        Assert.Equal(now.AddHours(8), ticket.DueAt);
    }
}
