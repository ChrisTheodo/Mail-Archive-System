using System.Security.Cryptography;
using MailArchive.Application.Abstractions;

namespace MailArchive.API.Storage;

public class AttachmentStorageService : IAttachmentStorageService
{
    private readonly IWebHostEnvironment _environment;

    public AttachmentStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<StoredAttachmentResult> SaveAsync(
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "attachment.bin";

        var sanitizedFileName = SanitizeFileName(Path.GetFileName(fileName));

        var storageRoot = Path.Combine(
            _environment.ContentRootPath,
            "storage",
            "attachments",
            "imported");

        Directory.CreateDirectory(storageRoot);

        var storedFileName = $"{Guid.NewGuid():N}_{sanitizedFileName}";
        var fullPath = Path.Combine(storageRoot, storedFileName);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        var relativeStoragePath = Path.Combine(
                "storage",
                "attachments",
                "imported",
                storedFileName)
            .Replace("\\", "/");

        var contentHash = CalculateSha256(content);

        return new StoredAttachmentResult(
            sanitizedFileName,
            contentType,
            content.LongLength,
            relativeStoragePath,
            contentHash);
    }

    private static string CalculateSha256(byte[] content)
    {
        var hashBytes = SHA256.HashData(content);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var sanitized = new string(
            fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "attachment.bin"
            : sanitized;
    }
}