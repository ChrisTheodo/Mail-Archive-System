using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Admin;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/dashboard")]
public class AdminDashboardMailboxUsageController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public AdminDashboardMailboxUsageController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet("mailbox-usage")]
    public async Task<IActionResult> GetMailboxUsage([FromQuery] int take = 20)
    {
        take = Math.Clamp(take, 1, 100);

        var mailboxes = await _db.Mailboxes
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

        var mailboxIds = mailboxes
            .Select(x => x.Id)
            .ToList();

        var emailStats = await _db.Emails
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .GroupBy(x => x.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalEmails = x.Count(),
                EmailsWithAttachments = x.Count(email => email.HasAttachments),
                LatestEmailReceivedAt = x.Max(email => (DateTime?)email.ReceivedAt)
            })
            .ToListAsync();

        var attachmentStats = await _db.Attachments
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.Email.MailboxId))
            .GroupBy(x => x.Email.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalAttachments = x.Count()
            })
            .ToListAsync();

        var importStats = await _db.ImportBatches
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .GroupBy(x => x.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalImportBatches = x.Count(),
                PendingImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Pending),
                QueuedImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Queued),
                RunningImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Running),
                CompletedImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Completed),
                CompletedWithErrorsImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.CompletedWithErrors),
                FailedImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Failed),
                CancelledImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Cancelled),
                LatestImportStartedAt = x.Max(importBatch => (DateTime?)importBatch.StartedAt)
            })
            .ToListAsync();

        var importErrorStats = await _db.ImportErrors
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.ImportBatch.MailboxId))
            .GroupBy(x => x.ImportBatch.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalImportErrors = x.Count()
            })
            .ToListAsync();

        var emailStatsByMailboxId = emailStats.ToDictionary(x => x.MailboxId);
        var attachmentStatsByMailboxId = attachmentStats.ToDictionary(x => x.MailboxId);
        var importStatsByMailboxId = importStats.ToDictionary(x => x.MailboxId);
        var importErrorStatsByMailboxId = importErrorStats.ToDictionary(x => x.MailboxId);

        var usageItems = mailboxes
            .Select(mailbox =>
            {
                emailStatsByMailboxId.TryGetValue(mailbox.Id, out var emailStat);
                attachmentStatsByMailboxId.TryGetValue(mailbox.Id, out var attachmentStat);
                importStatsByMailboxId.TryGetValue(mailbox.Id, out var importStat);
                importErrorStatsByMailboxId.TryGetValue(mailbox.Id, out var importErrorStat);

                return new AdminDashboardMailboxUsageItemResponse(
                    MailboxId: mailbox.Id,
                    MailboxDisplayName: mailbox.DisplayName,
                    OwnerUserId: mailbox.OwnerUserId,
                    OwnerEmail: mailbox.OwnerUser.Email,
                    OwnerIsActive: mailbox.OwnerUser.IsActive,
                    TotalEmails: emailStat?.TotalEmails ?? 0,
                    EmailsWithAttachments: emailStat?.EmailsWithAttachments ?? 0,
                    TotalAttachments: attachmentStat?.TotalAttachments ?? 0,
                    TotalImportBatches: importStat?.TotalImportBatches ?? 0,
                    PendingImports: importStat?.PendingImports ?? 0,
                    QueuedImports: importStat?.QueuedImports ?? 0,
                    RunningImports: importStat?.RunningImports ?? 0,
                    CompletedImports: importStat?.CompletedImports ?? 0,
                    CompletedWithErrorsImports: importStat?.CompletedWithErrorsImports ?? 0,
                    FailedImports: importStat?.FailedImports ?? 0,
                    CancelledImports: importStat?.CancelledImports ?? 0,
                    TotalImportErrors: importErrorStat?.TotalImportErrors ?? 0,
                    LatestEmailReceivedAt: emailStat?.LatestEmailReceivedAt,
                    LatestImportStartedAt: importStat?.LatestImportStartedAt
                );
            })
            .OrderByDescending(x => x.TotalEmails)
            .ThenByDescending(x => x.TotalImportBatches)
            .ThenBy(x => x.MailboxDisplayName)
            .Take(take)
            .ToList();

        var response = new AdminDashboardMailboxUsageResponse(
            TotalMailboxes: mailboxes.Count,
            ReturnedMailboxes: usageItems.Count,
            Mailboxes: usageItems,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "AdminDashboardMailboxUsageViewed",
            entityType: "Dashboard");

        return Ok(ApiResponse<AdminDashboardMailboxUsageResponse>.Ok(response));
    }
}