namespace MailArchive.Application.Contracts.Imports;

public record ImportErrorResponse(
    Guid Id,
    Guid ImportBatchId,
    string Message,
    DateTime CreatedAt
);