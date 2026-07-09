using System.Security.Claims;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Me;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/imports")]
public class MeImportDetailsController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public MeImportDetailsController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMyImportById(Guid id)
    {
        var currentUserIdResult = GetCurrentUserId();

        if (!currentUserIdResult.Success)
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserId = currentUserIdResult.UserId;

        var importBatch = await _db.ImportBatches
            .AsNoTracking()
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Mailbox.OwnerUserId == currentUserId);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        var errors = await _db.ImportErrors
            .AsNoTracking()
            .Where(x => x.ImportBatchId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MyImportErrorResponse(
                x.Id,
                x.ImportBatchId,
                x.Message,
                x.CreatedAt))
            .ToListAsync();

        var emailsInDatabase = await _db.Emails
            .AsNoTracking()
            .CountAsync(x => x.ImportBatchId == id);

        var emailsWithAttachments = await _db.Emails
            .AsNoTracking()
            .CountAsync(x => x.ImportBatchId == id && x.HasAttachments);

        var attachmentsInDatabase = await _db.Attachments
            .AsNoTracking()
            .CountAsync(x => x.Email.ImportBatchId == id);

        var response = new MyImportDetailsResponse(
            Id: importBatch.Id,
            PstFilename: importBatch.PstFilename,
            PstHash: importBatch.PstHash,
            PstStoragePath: importBatch.PstStoragePath,
            MailboxId: importBatch.MailboxId,
            MailboxDisplayName: importBatch.Mailbox.DisplayName,
            MailboxOwnerEmail: importBatch.Mailbox.OwnerUser?.Email,
            Status: importBatch.Status.ToString(),
            StartedAt: importBatch.StartedAt,
            CompletedAt: importBatch.CompletedAt,
            TotalMessages: importBatch.TotalMessages,
            ImportedMessages: importBatch.ImportedMessages,
            FailedMessages: importBatch.FailedMessages,
            EmailsInDatabase: emailsInDatabase,
            EmailsWithAttachments: emailsWithAttachments,
            AttachmentsInDatabase: attachmentsInDatabase,
            ErrorCount: errors.Count,
            ProgressPercent: CalculateProgressPercent(
                importBatch.Status,
                importBatch.TotalMessages,
                importBatch.ImportedMessages,
                importBatch.FailedMessages),
            IsCompleted: IsCompletedStatus(importBatch.Status),
            HasErrors: importBatch.FailedMessages > 0 || errors.Count > 0,
            Errors: errors,
            GeneratedAtUtc: DateTime.UtcNow);

        await _auditLogService.LogAsync(
            action: "MyImportDetailsViewed",
            entityType: "ImportBatch",
            entityId: importBatch.Id);

        return Ok(ApiResponse<MyImportDetailsResponse>.Ok(response));
    }

    [HttpGet("{id:guid}/progress")]
    public async Task<IActionResult> GetMyImportProgress(Guid id)
    {
        var currentUserIdResult = GetCurrentUserId();

        if (!currentUserIdResult.Success)
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserId = currentUserIdResult.UserId;

        var importBatch = await _db.ImportBatches
            .AsNoTracking()
            .Include(x => x.Mailbox)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Mailbox.OwnerUserId == currentUserId);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        var errorCount = await _db.ImportErrors
            .AsNoTracking()
            .CountAsync(x => x.ImportBatchId == id);

        var response = new MyImportProgressResponse(
            Id: importBatch.Id,
            PstFilename: importBatch.PstFilename,
            MailboxId: importBatch.MailboxId,
            MailboxDisplayName: importBatch.Mailbox.DisplayName,
            Status: importBatch.Status.ToString(),
            ProgressPercent: CalculateProgressPercent(
                importBatch.Status,
                importBatch.TotalMessages,
                importBatch.ImportedMessages,
                importBatch.FailedMessages),
            TotalMessages: importBatch.TotalMessages,
            ImportedMessages: importBatch.ImportedMessages,
            FailedMessages: importBatch.FailedMessages,
            ErrorCount: errorCount,
            IsCompleted: IsCompletedStatus(importBatch.Status),
            HasErrors: importBatch.FailedMessages > 0 || errorCount > 0,
            StartedAt: importBatch.StartedAt,
            CompletedAt: importBatch.CompletedAt);

        await _auditLogService.LogAsync(
            action: "MyImportProgressViewed",
            entityType: "ImportBatch",
            entityId: importBatch.Id);
        return Ok(ApiResponse<MyImportProgressResponse>.Ok(response));
    }

    [HttpGet("{id:guid}/errors")]
    public async Task<IActionResult> GetMyImportErrors(Guid id)
    {
        var currentUserIdResult = GetCurrentUserId();

        if (!currentUserIdResult.Success)
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserId = currentUserIdResult.UserId;

        var importExists = await _db.ImportBatches
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == id &&
                x.Mailbox.OwnerUserId == currentUserId);

        if (!importExists)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        var errors = await _db.ImportErrors
            .AsNoTracking()
            .Where(x => x.ImportBatchId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MyImportErrorResponse(
                x.Id,
                x.ImportBatchId,
                x.Message,
                x.CreatedAt))
            .ToListAsync();

        await _auditLogService.LogAsync(
            action: "MyImportErrorsViewed",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<IReadOnlyCollection<MyImportErrorResponse>>.Ok(errors));
    }

    private CurrentUserIdResult GetCurrentUserId()
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdText, out var currentUserId))
            return new CurrentUserIdResult(false, Guid.Empty);

        return new CurrentUserIdResult(true, currentUserId);
    }

    private static bool IsCompletedStatus(ImportBatchStatus status)
    {
        return status is
            ImportBatchStatus.Completed or
            ImportBatchStatus.CompletedWithErrors or
            ImportBatchStatus.Failed or
            ImportBatchStatus.Cancelled;
    }

    private static double CalculateProgressPercent(
        ImportBatchStatus status,
        int totalMessages,
        int importedMessages,
        int failedMessages)
    {
        if (status is ImportBatchStatus.Completed or
            ImportBatchStatus.CompletedWithErrors or
            ImportBatchStatus.Failed or
            ImportBatchStatus.Cancelled)
        {
            return 100;
        }

        if (status is ImportBatchStatus.Pending or ImportBatchStatus.Queued)
            return 0;

        if (totalMessages <= 0)
            return 0;

        var processedMessages = importedMessages + failedMessages;
        var progress = processedMessages / (double)totalMessages * 100;

        return Math.Round(Math.Clamp(progress, 0, 100), 2);
    }

    private readonly record struct CurrentUserIdResult(
        bool Success,
        Guid UserId);
}