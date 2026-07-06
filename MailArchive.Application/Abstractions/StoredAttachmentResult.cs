namespace MailArchive.Application.Abstractions;

public record StoredAttachmentResult(
    string FileName,
    string? ContentType,
    long SizeBytes,
    string StoragePath,
    string ContentHash
);