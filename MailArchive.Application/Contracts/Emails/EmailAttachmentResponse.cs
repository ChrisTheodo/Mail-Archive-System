namespace MailArchive.Application.Contracts.Emails;

public record EmailAttachmentResponse(
    Guid Id,
    string FileName,
    string? ContentType,
    long SizeBytes,
    string StoragePath,
    string ContentHash
);