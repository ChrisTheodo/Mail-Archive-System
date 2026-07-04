namespace MailArchive.Application.Contracts.Imports;

public record CreatePstImportRequest(
    Guid MailboxId,
    string PstFilename,
    string PstHash
);