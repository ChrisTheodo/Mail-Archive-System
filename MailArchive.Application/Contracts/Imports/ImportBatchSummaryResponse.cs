namespace MailArchive.Application.Contracts.Imports;

public record ImportBatchSummaryResponse(
    Guid Id,
    string PstFilename,
    string PstHash,
    string? PstStoragePath,
    Guid MailboxId,
    string? MailboxDisplayName,
    string? MailboxOwnerEmail,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages,
    int EmailsInDatabase,
    int EmailsWithAttachments,
    int AttachmentsInDatabase,
    int ErrorCount
);