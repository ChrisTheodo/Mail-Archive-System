namespace MailArchive.Application.Contracts.Imports;

public record ImportParserPreviewResponse(
    Guid ImportBatchId,
    string PstFilename,
    string ActiveParserProvider,
    int TotalParsedEmails,
    int ReturnedEmails,
    IReadOnlyCollection<ImportParserPreviewEmailResponse> Emails
);