namespace MailArchive.Application.Contracts.Imports;

public record ImportBatchResponse(
    Guid Id,
    string PstFilename,
    string PstHash,
    Guid MailboxId,
    string? MailboxDisplayName,
    string? MailboxOwnerEmail,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages
);