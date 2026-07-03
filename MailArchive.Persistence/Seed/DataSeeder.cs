using System.Text;
using MailArchive.Application.Auth;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Persistence.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(MailArchiveDbContext db)
    {
        await SeedUsersAsync(db);
        await SeedMailboxesAsync(db);
        await SeedImportBatchesAndEmailsAsync(db);
        await EnsureSeedAttachmentFilesAsync(db);
    }

    private static async Task SeedUsersAsync(MailArchiveDbContext db)
    {
        var passwordHasher = new Pbkdf2PasswordHasher();

        var adminEmail = "admin@example.com";
        var userEmail = "user@example.com";

        var admin = await db.Users.FirstOrDefaultAsync(x => x.Email == adminEmail);
        if (admin == null)
        {
            admin = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                DisplayName = "System Administrator",
                PasswordHash = passwordHasher.Hash("Admin123!"),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(admin);
        }
        else if (string.IsNullOrWhiteSpace(admin.PasswordHash))
        {
            admin.PasswordHash = passwordHasher.Hash("Admin123!");
            admin.UpdatedAt = DateTime.UtcNow;
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == userEmail);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = userEmail,
                DisplayName = "Test User",
                PasswordHash = passwordHasher.Hash("User123!"),
                Role = UserRole.User,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
        }
        else if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            user.PasswordHash = passwordHasher.Hash("User123!");
            user.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedMailboxesAsync(MailArchiveDbContext db)
    {
        var admin = await db.Users.FirstAsync(x => x.Email == "admin@example.com");
        var user = await db.Users.FirstAsync(x => x.Email == "user@example.com");

        var adminMailboxExists = await db.Mailboxes.AnyAsync(x =>
            x.OwnerUserId == admin.Id &&
            x.DisplayName == "Admin Archive");

        if (!adminMailboxExists)
        {
            db.Mailboxes.Add(new Mailbox
            {
                Id = Guid.NewGuid(),
                OwnerUserId = admin.Id,
                DisplayName = "Admin Archive",
                MailboxSource = MailboxSourceType.PST,
                IsAssigned = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var userMailboxExists = await db.Mailboxes.AnyAsync(x =>
            x.OwnerUserId == user.Id &&
            x.DisplayName == "User Archive");

        if (!userMailboxExists)
        {
            db.Mailboxes.Add(new Mailbox
            {
                Id = Guid.NewGuid(),
                OwnerUserId = user.Id,
                DisplayName = "User Archive",
                MailboxSource = MailboxSourceType.PST,
                IsAssigned = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedImportBatchesAndEmailsAsync(MailArchiveDbContext db)
    {
        var userMailbox = await db.Mailboxes
            .Include(x => x.OwnerUser)
            .FirstAsync(x => x.DisplayName == "User Archive");

        var adminMailbox = await db.Mailboxes
            .Include(x => x.OwnerUser)
            .FirstAsync(x => x.DisplayName == "Admin Archive");

        var userBatch = await GetOrCreateImportBatchAsync(
            db,
            userMailbox.Id,
            "user-test-archive.pst",
            "seed-user-pst-hash-0001"
        );

        var adminBatch = await GetOrCreateImportBatchAsync(
            db,
            adminMailbox.Id,
            "admin-test-archive.pst",
            "seed-admin-pst-hash-0001"
        );

        await SeedUserEmailsAsync(db, userMailbox, userBatch);
        await SeedAdminEmailsAsync(db, adminMailbox, adminBatch);

        await UpdateImportBatchMetricsAsync(db, userBatch.Id);
        await UpdateImportBatchMetricsAsync(db, adminBatch.Id);
    }

    private static async Task<ImportBatch> GetOrCreateImportBatchAsync(
        MailArchiveDbContext db,
        Guid mailboxId,
        string pstFilename,
        string pstHash)
    {
        var existingBatch = await db.ImportBatches.FirstOrDefaultAsync(x =>
            x.MailboxId == mailboxId &&
            x.PstHash == pstHash);

        if (existingBatch != null)
            return existingBatch;

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            MailboxId = mailboxId,
            PstFilename = pstFilename,
            PstHash = pstHash,
            Status = ImportBatchStatus.Completed,
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            CompletedAt = DateTime.UtcNow.AddMinutes(-10),
            TotalMessages = 0,
            ImportedMessages = 0,
            FailedMessages = 0
        };

        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        return batch;
    }

    private static async Task SeedUserEmailsAsync(
        MailArchiveDbContext db,
        Mailbox mailbox,
        ImportBatch batch)
    {
        await AddEmailIfMissingAsync(
            db,
            mailbox.Id,
            batch.Id,
            internetMessageId: "<seed-user-invoice-001@example.com>",
            messageHash: "seed-user-message-hash-0001",
            folderPath: "Inbox",
            senderEmail: "billing@vendor.com",
            senderName: "Vendor Billing",
            subject: "Invoice for June services",
            bodyText: "Hello, attached you will find the invoice for June services.",
            bodyHtml: "<p>Hello, attached you will find the invoice for June services.</p>",
            sentAt: DateTime.UtcNow.AddDays(-12),
            receivedAt: DateTime.UtcNow.AddDays(-12).AddMinutes(4),
            recipients: new List<EmailRecipient>
            {
                new EmailRecipient
                {
                    Id = Guid.NewGuid(),
                    RecipientType = RecipientType.To,
                    RecipientEmail = mailbox.OwnerUser.Email,
                    RecipientName = mailbox.OwnerUser.DisplayName
                },
                new EmailRecipient
                {
                    Id = Guid.NewGuid(),
                    RecipientType = RecipientType.Cc,
                    RecipientEmail = "accounts@example.com",
                    RecipientName = "Accounts"
                }
            },
            attachments: new List<Attachment>
            {
                new Attachment
                {
                    Id = Guid.NewGuid(),
                    FileName = "invoice-june.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 0,
                    StoragePath = "seed-storage/attachments/invoice-june.pdf",
                    ContentHash = "seed-attachment-hash-invoice-june",
                    CreatedAt = DateTime.UtcNow
                }
            }
        );

        await AddEmailIfMissingAsync(
            db,
            mailbox.Id,
            batch.Id,
            internetMessageId: "<seed-user-meeting-001@example.com>",
            messageHash: "seed-user-message-hash-0002",
            folderPath: "Inbox",
            senderEmail: "manager@example.com",
            senderName: "Project Manager",
            subject: "Project meeting notes",
            bodyText: "These are the notes from the latest project meeting about the archive system.",
            bodyHtml: "<p>These are the notes from the latest project meeting about the archive system.</p>",
            sentAt: DateTime.UtcNow.AddDays(-8),
            receivedAt: DateTime.UtcNow.AddDays(-8).AddMinutes(2),
            recipients: new List<EmailRecipient>
            {
                new EmailRecipient
                {
                    Id = Guid.NewGuid(),
                    RecipientType = RecipientType.To,
                    RecipientEmail = mailbox.OwnerUser.Email,
                    RecipientName = mailbox.OwnerUser.DisplayName
                }
            },
            attachments: new List<Attachment>()
        );

        await AddEmailIfMissingAsync(
            db,
            mailbox.Id,
            batch.Id,
            internetMessageId: "<seed-user-travel-001@example.com>",
            messageHash: "seed-user-message-hash-0003",
            folderPath: "Sent Items",
            senderEmail: mailbox.OwnerUser.Email,
            senderName: mailbox.OwnerUser.DisplayName,
            subject: "Travel documents",
            bodyText: "I am sending the travel documents for the upcoming business trip.",
            bodyHtml: "<p>I am sending the travel documents for the upcoming business trip.</p>",
            sentAt: DateTime.UtcNow.AddDays(-3),
            receivedAt: DateTime.UtcNow.AddDays(-3),
            recipients: new List<EmailRecipient>
            {
                new EmailRecipient
                {
                    Id = Guid.NewGuid(),
                    RecipientType = RecipientType.To,
                    RecipientEmail = "travel@example.com",
                    RecipientName = "Travel Office"
                }
            },
            attachments: new List<Attachment>
            {
                new Attachment
                {
                    Id = Guid.NewGuid(),
                    FileName = "travel-confirmation.pdf",
                    ContentType = "application/pdf",
                    SizeBytes = 0,
                    StoragePath = "seed-storage/attachments/travel-confirmation.pdf",
                    ContentHash = "seed-attachment-hash-travel-confirmation",
                    CreatedAt = DateTime.UtcNow
                }
            }
        );
    }

    private static async Task SeedAdminEmailsAsync(
        MailArchiveDbContext db,
        Mailbox mailbox,
        ImportBatch batch)
    {
        await AddEmailIfMissingAsync(
            db,
            mailbox.Id,
            batch.Id,
            internetMessageId: "<seed-admin-audit-001@example.com>",
            messageHash: "seed-admin-message-hash-0001",
            folderPath: "Inbox",
            senderEmail: "security@example.com",
            senderName: "Security Team",
            subject: "Audit log review",
            bodyText: "Please review the latest audit log export for the archive system.",
            bodyHtml: "<p>Please review the latest audit log export for the archive system.</p>",
            sentAt: DateTime.UtcNow.AddDays(-6),
            receivedAt: DateTime.UtcNow.AddDays(-6).AddMinutes(1),
            recipients: new List<EmailRecipient>
            {
                new EmailRecipient
                {
                    Id = Guid.NewGuid(),
                    RecipientType = RecipientType.To,
                    RecipientEmail = mailbox.OwnerUser.Email,
                    RecipientName = mailbox.OwnerUser.DisplayName
                }
            },
            attachments: new List<Attachment>()
        );

        await AddEmailIfMissingAsync(
            db,
            mailbox.Id,
            batch.Id,
            internetMessageId: "<seed-admin-import-001@example.com>",
            messageHash: "seed-admin-message-hash-0002",
            folderPath: "Inbox",
            senderEmail: "imports@example.com",
            senderName: "Import Service",
            subject: "PST import completed",
            bodyText: "The scheduled PST import completed successfully.",
            bodyHtml: "<p>The scheduled PST import completed successfully.</p>",
            sentAt: DateTime.UtcNow.AddDays(-2),
            receivedAt: DateTime.UtcNow.AddDays(-2).AddMinutes(3),
            recipients: new List<EmailRecipient>
            {
                new EmailRecipient
                {
                    Id = Guid.NewGuid(),
                    RecipientType = RecipientType.To,
                    RecipientEmail = mailbox.OwnerUser.Email,
                    RecipientName = mailbox.OwnerUser.DisplayName
                }
            },
            attachments: new List<Attachment>()
        );
    }

    private static async Task AddEmailIfMissingAsync(
        MailArchiveDbContext db,
        Guid mailboxId,
        Guid importBatchId,
        string internetMessageId,
        string messageHash,
        string folderPath,
        string senderEmail,
        string senderName,
        string subject,
        string bodyText,
        string bodyHtml,
        DateTime sentAt,
        DateTime receivedAt,
        List<EmailRecipient> recipients,
        List<Attachment> attachments)
    {
        var exists = await db.Emails.AnyAsync(x =>
            x.MailboxId == mailboxId &&
            x.MessageHash == messageHash);

        if (exists)
            return;

        var email = new Email
        {
            Id = Guid.NewGuid(),
            MailboxId = mailboxId,
            ImportBatchId = importBatchId,
            InternetMessageId = internetMessageId,
            MessageHash = messageHash,
            FolderPath = folderPath,
            SenderEmail = senderEmail,
            SenderName = senderName,
            Subject = subject,
            BodyText = bodyText,
            BodyHtml = bodyHtml,
            SentAt = sentAt,
            ReceivedAt = receivedAt,
            HasAttachments = attachments.Count > 0,
            CreatedAt = DateTime.UtcNow,
            Recipients = recipients,
            Attachments = attachments
        };

        db.Emails.Add(email);
        await db.SaveChangesAsync();
    }

    private static async Task UpdateImportBatchMetricsAsync(
        MailArchiveDbContext db,
        Guid importBatchId)
    {
        var batch = await db.ImportBatches.FirstAsync(x => x.Id == importBatchId);

        var totalMessages = await db.Emails.CountAsync(x => x.ImportBatchId == importBatchId);

        batch.TotalMessages = totalMessages;
        batch.ImportedMessages = totalMessages;
        batch.FailedMessages = 0;
        batch.Status = ImportBatchStatus.Completed;
        batch.CompletedAt ??= DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private static async Task EnsureSeedAttachmentFilesAsync(MailArchiveDbContext db)
    {
        var seedFiles = new List<SeedAttachmentFile>
        {
            new SeedAttachmentFile(
                ContentHash: "seed-attachment-hash-invoice-june",
                FileName: "invoice-june.pdf",
                ContentType: "application/pdf",
                Title: "Invoice for June services"
            ),
            new SeedAttachmentFile(
                ContentHash: "seed-attachment-hash-travel-confirmation",
                FileName: "travel-confirmation.pdf",
                ContentType: "application/pdf",
                Title: "Travel confirmation"
            )
        };

        var storageRoot = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "seed-storage", "attachments"));

        Directory.CreateDirectory(storageRoot);

        foreach (var seedFile in seedFiles)
        {
            var absolutePath = Path.Combine(storageRoot, seedFile.FileName);
            var fileBytes = CreateMinimalPdfBytes(seedFile.Title);

            await File.WriteAllBytesAsync(absolutePath, fileBytes);

            var attachment = await db.Attachments.FirstOrDefaultAsync(x =>
                x.ContentHash == seedFile.ContentHash);

            if (attachment == null)
                continue;

            attachment.FileName = seedFile.FileName;
            attachment.ContentType = seedFile.ContentType;
            attachment.StoragePath = absolutePath;
            attachment.SizeBytes = fileBytes.Length;
        }

        await db.SaveChangesAsync();
    }

    private static byte[] CreateMinimalPdfBytes(string title)
    {
        var safeTitle = title
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");

        var pdf = $"""
                   %PDF-1.1
                   1 0 obj
                   << /Type /Catalog /Pages 2 0 R >>
                   endobj
                   2 0 obj
                   << /Type /Pages /Kids [3 0 R] /Count 1 >>
                   endobj
                   3 0 obj
                   << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>
                   endobj
                   4 0 obj
                   << /Length 72 >>
                   stream
                   BT
                   /F1 18 Tf
                   50 750 Td
                   (Seed attachment: {safeTitle}) Tj
                   ET
                   endstream
                   endobj
                   %%EOF
                   """;

        return Encoding.UTF8.GetBytes(pdf);
    }

    private sealed record SeedAttachmentFile(
        string ContentHash,
        string FileName,
        string ContentType,
        string Title);
}