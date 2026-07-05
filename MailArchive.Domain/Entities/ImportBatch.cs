using MailArchive.Domain.Enums;

namespace MailArchive.Domain.Entities;

public class ImportBatch
{
    public Guid Id { get; set; }

    public string PstFilename { get; set; } = string.Empty;

    public string PstHash { get; set; } = string.Empty;

    public string? PstStoragePath { get; set; }

    public Guid MailboxId { get; set; }

    public Mailbox Mailbox { get; set; } = null!;

    public ImportBatchStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int TotalMessages { get; set; }

    public int ImportedMessages { get; set; }

    public int FailedMessages { get; set; }

    public ICollection<Email> Emails { get; set; } = new List<Email>();
}