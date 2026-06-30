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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Mailboxes)
            .WithOne(m => m.OwnerUser)
            .HasForeignKey(m => m.OwnerUserId);

        modelBuilder.Entity<Mailbox>()
            .HasMany(m => m.Emails)
            .WithOne(e => e.Mailbox)
            .HasForeignKey(e => e.MailboxId);

        modelBuilder.Entity<ImportBatch>()
            .HasMany(b => b.Emails)
            .WithOne(e => e.ImportBatch)
            .HasForeignKey(e => e.ImportBatchId);

        modelBuilder.Entity<Email>()
            .HasMany(e => e.Attachments)
            .WithOne(a => a.Email)
            .HasForeignKey(a => a.EmailId);

        modelBuilder.Entity<Email>()
            .HasMany(e => e.Recipients)
            .WithOne(r => r.Email)
            .HasForeignKey(r => r.EmailId);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId);

        modelBuilder.Entity<Email>().HasIndex(e => e.MailboxId);
        modelBuilder.Entity<Email>().HasIndex(e => e.SenderEmail);
        modelBuilder.Entity<Email>().HasIndex(e => e.ReceivedAt);
        modelBuilder.Entity<Email>().HasIndex(e => e.InternetMessageId);

        modelBuilder.Entity<EmailRecipient>().HasIndex(r => r.RecipientEmail);
        modelBuilder.Entity<Attachment>().HasIndex(a => a.FileName);
    }
}