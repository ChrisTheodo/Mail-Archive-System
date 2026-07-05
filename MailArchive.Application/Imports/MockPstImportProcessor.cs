using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Imports;

public class MockPstImportProcessor : IPstImportProcessor
{
    private readonly IMailArchiveDbContext _db;
    private readonly IStoragePathResolver _storagePathResolver;

    public MockPstImportProcessor(
        IMailArchiveDbContext db,
        IStoragePathResolver storagePathResolver)
    {
        _db = db;
        _storagePathResolver = storagePathResolver;
    }

    public async Task<Result<ImportBatch>> ProcessAsync(Guid importBatchId)
    {
        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == importBatchId);

        if (importBatch == null)
            return Result<ImportBatch>.Failure("ImportBatchNotFound");

        if (importBatch.Status == ImportBatchStatus.Completed ||
            importBatch.Status == ImportBatchStatus.CompletedWithErrors)
        {
            return Result<ImportBatch>.Failure("ImportBatchAlreadyCompleted");
        }

        if (importBatch.Status == ImportBatchStatus.Failed)
            return Result<ImportBatch>.Failure("ImportBatchAlreadyFailed");

        if (string.IsNullOrWhiteSpace(importBatch.PstStoragePath))
            return await FailProcessingAsync(importBatch, "PstStoragePathMissing");

        var pstFilePath = _storagePathResolver.ResolvePath(importBatch.PstStoragePath);

        if (!File.Exists(pstFilePath))
            return await FailProcessingAsync(importBatch, "PstFileNotFound");

        importBatch.Status = ImportBatchStatus.Running;
        importBatch.StartedAt = DateTime.UtcNow;
        importBatch.CompletedAt = null;

        await _db.SaveChangesAsync();

        var pstFileInfo = new FileInfo(pstFilePath);

        var mockEmails = CreateMockEmails(importBatch, pstFileInfo.Length);

        var importedMessages = 0;

        foreach (var email in mockEmails)
        {
            var exists = await _db.Emails.AnyAsync(x =>
                x.MailboxId == email.MailboxId &&
                x.MessageHash == email.MessageHash);

            if (exists)
                continue;

            _db.Emails.Add(email);
            importedMessages++;
        }

        await _db.SaveChangesAsync();

        importBatch.TotalMessages = mockEmails.Count;
        importBatch.ImportedMessages = importedMessages;
        importBatch.FailedMessages = 0;
        importBatch.CompletedAt = DateTime.UtcNow;
        importBatch.Status = ImportBatchStatus.Completed;

        await _db.SaveChangesAsync();

        var completedImport = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstAsync(x => x.Id == importBatch.Id);

        return Result<ImportBatch>.Success(completedImport);
    }

    private async Task<Result<ImportBatch>> FailProcessingAsync(
        ImportBatch importBatch,
        string errorMessage)
    {
        importBatch.Status = ImportBatchStatus.Failed;
        importBatch.CompletedAt = DateTime.UtcNow;
        importBatch.TotalMessages = Math.Max(importBatch.TotalMessages, 0);
        importBatch.ImportedMessages = Math.Max(importBatch.ImportedMessages, 0);
        importBatch.FailedMessages = Math.Max(importBatch.FailedMessages, 1);

        _db.ImportErrors.Add(new ImportError
        {
            Id = Guid.NewGuid(),
            ImportBatchId = importBatch.Id,
            Message = errorMessage,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Result<ImportBatch>.Failure(errorMessage);
    }

    private static List<Email> CreateMockEmails(ImportBatch importBatch, long pstFileSizeBytes)
    {
        var mailbox = importBatch.Mailbox;
        var owner = mailbox.OwnerUser;

        var now = DateTime.UtcNow;

        return new List<Email>
        {
            new Email
            {
                Id = Guid.NewGuid(),
                MailboxId = importBatch.MailboxId,
                ImportBatchId = importBatch.Id,
                InternetMessageId = $"<mock-import-{importBatch.Id:N}-001@mailarchive.local>",
                MessageHash = $"mock-import-{importBatch.Id:N}-001",
                FolderPath = "Inbox",
                SenderEmail = "mock.sender@example.com",
                SenderName = "Mock Sender",
                Subject = $"Mock imported email 1 from {importBatch.PstFilename}",
                BodyText = $"This is a mock imported email created from uploaded PST file {importBatch.PstFilename}. File size: {pstFileSizeBytes} bytes.",
                BodyHtml = $"<p>This is a mock imported email created from uploaded PST file {importBatch.PstFilename}. File size: {pstFileSizeBytes} bytes.</p>",
                SentAt = now.AddMinutes(-30),
                ReceivedAt = now.AddMinutes(-29),
                HasAttachments = false,
                CreatedAt = now,
                Recipients = new List<EmailRecipient>
                {
                    new EmailRecipient
                    {
                        Id = Guid.NewGuid(),
                        RecipientType = RecipientType.To,
                        RecipientEmail = owner.Email,
                        RecipientName = owner.DisplayName
                    }
                }
            },
            new Email
            {
                Id = Guid.NewGuid(),
                MailboxId = importBatch.MailboxId,
                ImportBatchId = importBatch.Id,
                InternetMessageId = $"<mock-import-{importBatch.Id:N}-002@mailarchive.local>",
                MessageHash = $"mock-import-{importBatch.Id:N}-002",
                FolderPath = "Inbox",
                SenderEmail = "mock.notifications@example.com",
                SenderName = "Mock Notifications",
                Subject = $"Mock imported email 2 from {importBatch.PstFilename}",
                BodyText = $"This is the second mock email generated from uploaded PST file {importBatch.PstFilename}. File size: {pstFileSizeBytes} bytes.",
                BodyHtml = $"<p>This is the second mock email generated from uploaded PST file {importBatch.PstFilename}. File size: {pstFileSizeBytes} bytes.</p>",
                SentAt = now.AddMinutes(-20),
                ReceivedAt = now.AddMinutes(-19),
                HasAttachments = false,
                CreatedAt = now,
                Recipients = new List<EmailRecipient>
                {
                    new EmailRecipient
                    {
                        Id = Guid.NewGuid(),
                        RecipientType = RecipientType.To,
                        RecipientEmail = owner.Email,
                        RecipientName = owner.DisplayName
                    }
                }
            }
        };
    }
}