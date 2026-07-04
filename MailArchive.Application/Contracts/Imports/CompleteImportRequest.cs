namespace MailArchive.Application.Contracts.Imports;

public record CompleteImportRequest(
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages
);