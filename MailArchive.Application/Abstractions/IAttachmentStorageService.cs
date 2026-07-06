namespace MailArchive.Application.Abstractions;

public interface IAttachmentStorageService
{
    Task<StoredAttachmentResult> SaveAsync(
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default);
}