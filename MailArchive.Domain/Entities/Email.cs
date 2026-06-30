namespace MailArchive.Domain.Entities;

public class Email
{
    public Guid Id { get; set; }

    // Ownership / isolation
    public Guid MailboxId { get; set; }
    public Mailbox Mailbox { get; set; } = null!;

    public Guid ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;

    // Identity from email system
    public string? InternetMessageId { get; set; }

    // Deduplication key (your system-level hash)
    public string MessageHash { get; set; } = null!;

    // Folder inside PST
    public string? FolderPath { get; set; }

    // Sender
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }

    // Content
    public string? Subject { get; set; }
    public string? BodyText { get; set; }
    public string? BodyHtml { get; set; }

    // Dates
    public DateTime? SentAt { get; set; }
    public DateTime? ReceivedAt { get; set; }

    // Flags
    public bool HasAttachments { get; set; }

    // System metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<EmailRecipient> Recipients { get; set; } = new List<EmailRecipient>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}