using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sidecil.Tickets.Domain;

namespace Sidecil.Tickets.Infrastructure;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Requester";
    public Guid OrganizationId { get; set; }
    public Guid? TeamId { get; set; }
}

public sealed class TicketsDbContext(DbContextOptions<TicketsDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ChatLoginAttempt> ChatLoginAttempts => Set<ChatLoginAttempt>();
    public DbSet<PortalLoginAttempt> PortalLoginAttempts => Set<PortalLoginAttempt>();
    public DbSet<ChatSite> ChatSites => Set<ChatSite>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> Messages => Set<TicketMessage>();
    public DbSet<TicketEvent> Events => Set<TicketEvent>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<OutboundEmail> OutboundEmails => Set<OutboundEmail>();
    public DbSet<IncomingEmail> IncomingEmails => Set<IncomingEmail>();
    public DbSet<GuestAttachment> GuestAttachments => Set<GuestAttachment>();
    public DbSet<GuestSubmission> GuestSubmissions => Set<GuestSubmission>();
    public DbSet<MailboxCursor> MailboxCursors => Set<MailboxCursor>();
    public static readonly Guid GuestOrganizationId = new("91563faf-00a3-49ac-b2d0-0e8c702f4d41");

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<PortalLoginAttempt>(e => {
            e.ToTable("ExternalLoginAttempts", "identity");
            e.Property(x => x.Provider).HasMaxLength(20);
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.Property(x => x.SecurityStamp).HasMaxLength(128);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.LinkUserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<ChatLoginAttempt>(e => {
            e.ToTable("LoginAttempts", "chat");
            e.Property(x => x.Provider).HasMaxLength(20);
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne<ChatConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<ChatConversation>().Property(x => x.VerificationHash).HasMaxLength(64);
        model.Entity<ChatConversation>().Property(x => x.IdentityProvider).HasMaxLength(20);
        model.Entity<ChatConversation>().Property(x => x.IdentityIssuer).HasMaxLength(300);
        model.Entity<ChatConversation>().Property(x => x.IdentitySubject).HasMaxLength(255);
        model.Entity<ChatSite>(e => {
            e.ToTable("Sites", "chat");
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Origin).HasMaxLength(300);
        });
        model.Entity<ChatConversation>(e => {
            e.ToTable("Conversations", "chat");
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Module).HasMaxLength(120);
            e.Property(x => x.Subject).HasMaxLength(180);
            e.Property(x => x.Body).HasMaxLength(8000);
            e.Property(x => x.Category).HasMaxLength(60);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne<ChatSite>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GuestSubmission>().WithMany().HasForeignKey(x => x.GuestSubmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.GuestSubmissionId).IsUnique().HasFilter("[GuestSubmissionId] IS NOT NULL");
        });
        model.Entity<ChatAttachment>(e => {
            e.ToTable("Attachments", "chat", t => t.HasCheckConstraint("CK_ChatAttachment_Size", "[Length] > 0 AND [Length] <= 5242880 AND DATALENGTH([Content]) = [Length]"));
            e.Property(x => x.FileName).HasMaxLength(180);
            e.Property(x => x.ContentType).HasMaxLength(80);
            e.HasOne<ChatConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Organization>().HasData(new Organization { Id = GuestOrganizationId, Name = "Solicitudes externas" });
        model.Entity<Ticket>().Property(x => x.GuestName).HasMaxLength(120);
        model.Entity<Ticket>().Property(x => x.GuestEmail).HasMaxLength(200);
        model.Entity<TicketMessage>().Property(x => x.AuthorName).HasMaxLength(120);
        model.Entity<TicketMessage>().Property(x => x.Source).HasMaxLength(20);
        model.Entity<TicketEvent>().Property(x => x.ActorName).HasMaxLength(120);
        model.Entity<EmailTemplate>(e => {
            e.ToTable("Templates", "communications");
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(8000);
            e.Property(x => x.Signature).HasMaxLength(500);
            e.Property(x => x.UpdatedBy).HasMaxLength(450);
            e.Property(x => x.Version).IsRowVersion();
            e.HasData(new EmailTemplate { Id = 1, UpdatedAt = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc) });
        });
        model.Entity<OutboundEmail>(e => {
            e.ToTable("Outbound", "communications");
            e.Property(x => x.DeduplicationKey).HasMaxLength(100);
            e.HasIndex(x => x.DeduplicationKey).IsUnique();
            e.Property(x => x.MessageId).HasMaxLength(200);
            e.HasIndex(x => x.MessageId).IsUnique();
            e.Property(x => x.Recipient).HasMaxLength(254);
            e.Property(x => x.Subject).HasMaxLength(500);
            e.Property(x => x.Body).HasMaxLength(16000);
            e.Property(x => x.State).HasMaxLength(20);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.LastError).HasMaxLength(200);
            e.HasIndex(x => new { x.State, x.NextAttemptAt });
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<IncomingEmail>(e => {
            e.ToTable("Inbound", "communications");
            e.Property(x => x.SourceKey).HasMaxLength(64);
            e.Property(x => x.MessageKey).HasMaxLength(64);
            e.HasIndex(x => x.SourceKey).IsUnique();
            e.HasIndex(x => x.MessageKey).IsUnique();
            e.Property(x => x.Sender).HasMaxLength(254);
            e.Property(x => x.Subject).HasMaxLength(500);
            e.Property(x => x.Body).HasMaxLength(12000);
            e.Property(x => x.State).HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<GuestAttachment>(e => {
            e.ToTable("GuestAttachments", "communications", t => t.HasCheckConstraint("CK_GuestAttachment_Size", "[Length] > 0 AND [Length] <= 5242880 AND DATALENGTH([Content]) = [Length]"));
            e.Property(x => x.FileName).HasMaxLength(180);
            e.Property(x => x.ContentType).HasMaxLength(80);
            e.HasOne<GuestSubmission>().WithMany().HasForeignKey(x => x.GuestSubmissionId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<GuestSubmission>(e => {
            e.ToTable("GuestSubmissions", "communications");
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Subject).HasMaxLength(180);
            e.Property(x => x.Body).HasMaxLength(12000);
            e.Property(x => x.Category).HasMaxLength(60);
            e.HasIndex(x => new { x.Email, x.CreatedAt });
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MailboxCursor>(e => {
            e.ToTable("MailboxCursors", "communications");
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<ApplicationUser>(e => {
            e.Property(x => x.DisplayName).HasMaxLength(120);
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Organization>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<Team>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<Ticket>(e => {
            e.ToTable("Tickets", "tickets");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublicId).IsUnique();
            e.Property(x => x.Subject).HasMaxLength(180);
            e.Property(x => x.Category).HasMaxLength(60);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.TeamId, x.Status, x.UpdatedAt, x.Id });
            e.HasIndex(x => new { x.OrganizationId, x.RequesterId, x.UpdatedAt, x.Id });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TicketAttachment>(e => {
            e.ToTable("Attachments", "tickets", t => t.HasCheckConstraint("CK_TicketAttachment_Size", "[Length] > 0 AND [Length] <= 5242880 AND DATALENGTH([Content]) = [Length]"));
            e.Property(x => x.FileName).HasMaxLength(180);
            e.Property(x => x.ContentType).HasMaxLength(80);
            e.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<TicketMessage>(e => {
            e.ToTable("Messages", "tickets");
            e.Property(x => x.Body).HasMaxLength(12000);
            e.HasIndex(x => new { x.TicketId, x.CreatedAt, x.Id });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TicketEvent>(e => {
            e.ToTable("Events", "audit");
            e.Property(x => x.Kind).HasMaxLength(40);
            e.Property(x => x.Detail).HasMaxLength(2200);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TicketsDbContext>
{
    public TicketsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<TicketsDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__Tickets") ??
            @"Server=(localdb)\MSSQLLocalDB;Database=SidecilTicketsDev;Trusted_Connection=True;TrustServerCertificate=True")
        .Options);
}
