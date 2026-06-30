using MailArchive.Domain.Enums;

namespace MailArchive.Domain.Entities;

public class ImportBatch
{
    public Guid Id { get; set; }

    // Source file info
    public string PstFilename { get; set; } = null!;
    public string PstHash { get; set; } = null!;

    // Relation
    public Guid MailboxId { get; set; }
    public Mailbox Mailbox { get; set; } = null!;

    // Status tracking
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Pending;

    // Timing
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Metrics
    public int TotalMessages { get; set; }
    public int ImportedMessages { get; set; }
    public int FailedMessages { get; set; }
    
    public ICollection<Email> Emails { get; set; } = new List<Email>();
}