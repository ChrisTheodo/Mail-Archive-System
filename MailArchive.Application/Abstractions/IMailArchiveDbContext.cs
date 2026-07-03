using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Abstractions;

public interface IMailArchiveDbContext
{
    DbSet<User> Users { get; }

    DbSet<Mailbox> Mailboxes { get; }

    DbSet<Email> Emails { get; }

    DbSet<EmailRecipient> EmailRecipients { get; }

    DbSet<Attachment> Attachments { get; }

    DbSet<ImportBatch> ImportBatches { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}