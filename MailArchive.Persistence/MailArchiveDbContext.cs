using MailArchive.Application.Abstractions;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Persistence;

public class MailArchiveDbContext : DbContext, IMailArchiveDbContext
{
    public MailArchiveDbContext(DbContextOptions<MailArchiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();

    public DbSet<Email> Emails => Set<Email>();

    public DbSet<EmailRecipient> EmailRecipients => Set<EmailRecipient>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportError> ImportErrors => Set<ImportError>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MailArchiveDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}