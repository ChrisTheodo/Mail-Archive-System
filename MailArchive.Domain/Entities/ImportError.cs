namespace MailArchive.Domain.Entities;

public class ImportError
{
    public Guid Id { get; set; }

    public Guid ImportBatchId { get; set; }

    public ImportBatch ImportBatch { get; set; } = null!;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}