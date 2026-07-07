namespace MailArchive.Application.Contracts.Imports;

public record ImportParserInspectionResponse(
    Guid ImportBatchId,
    string PstFilename,
    string ActiveParserProvider,
    int TotalEmails,
    int EmailsWithAttachments,
    int TotalAttachments,
    int TotalRecipients,
    int FolderCount,
    IReadOnlyCollection<string> Folders,
    DateTime? EarliestEmailDate,
    DateTime? LatestEmailDate
);