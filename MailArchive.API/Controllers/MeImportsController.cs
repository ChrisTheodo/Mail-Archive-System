using System.Security.Claims;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Me;
using MailArchive.Application.Me.Queries;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/imports")]
public class MeImportsController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public MeImportsController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyImports([FromQuery] MyImportQueryParameters parameters)
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdText, out var currentUserId))
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserEmail = User.FindFirstValue(ClaimTypes.Email);
        var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

        var page = Math.Max(parameters.Page, 1);
        var pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        ImportBatchStatus? statusFilter = null;

        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            if (!Enum.TryParse<ImportBatchStatus>(
                    parameters.Status.Trim(),
                    ignoreCase: true,
                    out var parsedStatus))
            {
                return BadRequest(ApiResponse<string>.Fail("InvalidImportStatus"));
            }

            statusFilter = parsedStatus;
        }

        var myMailboxIds = await _db.Mailboxes
            .AsNoTracking()
            .Where(x => x.OwnerUserId == currentUserId)
            .Select(x => x.Id)
            .ToListAsync();

        var query = _db.ImportBatches
            .AsNoTracking()
            .Where(x => myMailboxIds.Contains(x.MailboxId));

        if (parameters.MailboxId.HasValue)
        {
            query = query.Where(x => x.MailboxId == parameters.MailboxId.Value);
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(x => x.Status == statusFilter.Value);
        }

        var totalCount = await query.CountAsync();

        var importRows = await query
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.PstFilename,
                x.MailboxId,
                MailboxDisplayName = x.Mailbox.DisplayName,
                x.Status,
                x.StartedAt,
                x.CompletedAt,
                x.TotalMessages,
                x.ImportedMessages,
                x.FailedMessages
            })
            .ToListAsync();

        var importIds = importRows
            .Select(x => x.Id)
            .ToList();

        var errorCounts = await _db.ImportErrors
            .AsNoTracking()
            .Where(x => importIds.Contains(x.ImportBatchId))
            .GroupBy(x => x.ImportBatchId)
            .Select(x => new
            {
                ImportBatchId = x.Key,
                ErrorCount = x.Count()
            })
            .ToListAsync();

        var errorCountsByImportId = errorCounts
            .ToDictionary(x => x.ImportBatchId, x => x.ErrorCount);

        var items = importRows
            .Select(importRow =>
            {
                errorCountsByImportId.TryGetValue(importRow.Id, out var errorCount);

                return new MyImportResponse(
                    Id: importRow.Id,
                    PstFilename: importRow.PstFilename,
                    MailboxId: importRow.MailboxId,
                    MailboxDisplayName: importRow.MailboxDisplayName,
                    Status: importRow.Status.ToString(),
                    StartedAt: importRow.StartedAt,
                    CompletedAt: importRow.CompletedAt,
                    TotalMessages: importRow.TotalMessages,
                    ImportedMessages: importRow.ImportedMessages,
                    FailedMessages: importRow.FailedMessages,
                    ErrorCount: errorCount,
                    ProgressPercent: CalculateProgressPercent(importRow.Status, importRow.TotalMessages, importRow.ImportedMessages, importRow.FailedMessages),
                    IsCompleted: IsCompletedStatus(importRow.Status),
                    HasErrors: importRow.FailedMessages > 0 || errorCount > 0
                );
            })
            .ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new MyImportListResponse(
            CurrentUserId: currentUserId,
            CurrentUserEmail: currentUserEmail,
            CurrentUserRole: currentUserRole,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            Items: items,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "MyImportsViewed",
            entityType: "ImportBatch");

        return Ok(ApiResponse<MyImportListResponse>.Ok(response));
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
}