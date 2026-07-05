namespace MailArchive.Application.Contracts.Imports;

public record FailImportRequest(
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages,
    string? ErrorMessage = null
);