using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Imports;

public class MockPstImportProcessor : IPstImportProcessor
{
    private readonly IMailArchiveDbContext _db;

    public MockPstImportProcessor(IMailArchiveDbContext db)
    {
        _db = db;
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

        importBatch.Status = ImportBatchStatus.Running;
        importBatch.StartedAt = DateTime.UtcNow;
        importBatch.CompletedAt = null;

        await _db.SaveChangesAsync();

        var mockEmails = CreateMockEmails(importBatch);

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

    private static List<Email> CreateMockEmails(ImportBatch importBatch)
    {
        var mailbox = importBatch.Mailbox;
        var owner = mailbox.OwnerUser;

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
                BodyText = "This is a mock imported email created by the mock PST import processor.",
                BodyHtml = "<p>This is a mock imported email created by the mock PST import processor.</p>",
                SentAt = DateTime.UtcNow.AddMinutes(-30),
                ReceivedAt = DateTime.UtcNow.AddMinutes(-29),
                HasAttachments = false,
                CreatedAt = DateTime.UtcNow,
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
                BodyText = "This is the second mock email generated for import pipeline testing.",
                BodyHtml = "<p>This is the second mock email generated for import pipeline testing.</p>",
                SentAt = DateTime.UtcNow.AddMinutes(-20),
                ReceivedAt = DateTime.UtcNow.AddMinutes(-19),
                HasAttachments = false,
                CreatedAt = DateTime.UtcNow,
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