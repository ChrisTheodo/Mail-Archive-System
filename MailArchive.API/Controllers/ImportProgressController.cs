using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/imports")]
public class ImportProgressController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;

    public ImportProgressController(IMailArchiveDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:guid}/progress")]
    public async Task<IActionResult> GetImportProgress(Guid id)
    {
        var importBatch = await _db.ImportBatches
            .AsNoTracking()
            .Include(x => x.Mailbox)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        var errorCount = await _db.ImportErrors
            .AsNoTracking()
            .CountAsync(x => x.ImportBatchId == id);

        var isCompleted = IsCompletedStatus(importBatch.Status);

        var hasErrors =
            importBatch.Status == ImportBatchStatus.CompletedWithErrors ||
            importBatch.Status == ImportBatchStatus.Failed ||
            importBatch.FailedMessages > 0 ||
            errorCount > 0;

        var response = new ImportProgressResponse(
            importBatch.Id,
            importBatch.PstFilename,
            importBatch.MailboxId,
            importBatch.Mailbox?.DisplayName,
            importBatch.Status.ToString(),
            CalculateProgressPercent(
                importBatch.Status,
                importBatch.TotalMessages,
                importBatch.ImportedMessages,
                importBatch.FailedMessages),
            importBatch.TotalMessages,
            importBatch.ImportedMessages,
            importBatch.FailedMessages,
            errorCount,
            isCompleted,
            hasErrors,
            importBatch.StartedAt,
            importBatch.CompletedAt
        );

        return Ok(ApiResponse<ImportProgressResponse>.Ok(response));
    }

    private static bool IsCompletedStatus(ImportBatchStatus status)
    {
        return status == ImportBatchStatus.Completed ||
               status == ImportBatchStatus.CompletedWithErrors ||
               status == ImportBatchStatus.Failed ||
               status == ImportBatchStatus.Cancelled;
    }

    private static int CalculateProgressPercent(
        ImportBatchStatus status,
        int totalMessages,
        int importedMessages,
        int failedMessages)
    {
        if (status == ImportBatchStatus.Completed ||
            status == ImportBatchStatus.CompletedWithErrors ||
            status == ImportBatchStatus.Failed ||
            status == ImportBatchStatus.Cancelled)
        {
            return 100;
        }

        if (status == ImportBatchStatus.Pending)
            return 0;

        if (totalMessages <= 0)
            return 0;

        var processedMessages = importedMessages + failedMessages;

        var percent = (int)Math.Round(
            processedMessages * 100.0 / totalMessages,
            MidpointRounding.AwayFromZero);

        return Math.Clamp(percent, 0, 100);
    }
}