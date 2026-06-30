namespace MailArchive.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; }

    // Relation
    public Guid EmailId { get; set; }
    public Email Email { get; set; } = null!;

    // File metadata
    public string FileName { get; set; } = null!;
    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    // Storage
    public string StoragePath { get; set; } = null!;

    // Deduplication / integrity
    public string ContentHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}