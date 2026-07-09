namespace MailArchive.Application.Contracts.Me;

public record MyImportErrorResponse(
    Guid Id,
    Guid ImportBatchId,
    string Message,
    DateTime CreatedAt
);