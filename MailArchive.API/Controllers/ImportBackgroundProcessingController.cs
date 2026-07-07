using MailArchive.API.Background;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Imports;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/imports")]
public class ImportBackgroundProcessingController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;

    public ImportBackgroundProcessingController(
        IMailArchiveDbContext db,
        IBackgroundTaskQueue taskQueue,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser)
    {
        _db = db;
        _taskQueue = taskQueue;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
    }

    [HttpPost("{id:guid}/process/background")]
    public async Task<IActionResult> ProcessInBackground(Guid id)
    {
        var importBatch = await _db.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        if (importBatch.Status == ImportBatchStatus.Queued)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyQueued"));

        if (importBatch.Status == ImportBatchStatus.Running)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyRunning"));

        if (importBatch.Status == ImportBatchStatus.Completed ||
            importBatch.Status == ImportBatchStatus.CompletedWithErrors)
        {
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCompleted"));
        }

        if (importBatch.Status == ImportBatchStatus.Failed)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyFailed"));

        if (importBatch.Status == ImportBatchStatus.Cancelled)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCancelled"));

        var queuedByUserId = _currentUser.UserId;

        importBatch.Status = ImportBatchStatus.Queued;
        importBatch.StartedAt = DateTime.UtcNow;
        importBatch.CompletedAt = null;

        await _db.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "ImportProcessingQueued",
            entityType: "ImportBatch",
            entityId: id);

        await _taskQueue.QueueBackgroundWorkItemAsync(async (services, cancellationToken) =>
        {
            using var scope = services.CreateScope();

            var processor = scope.ServiceProvider.GetRequiredService<IPstImportProcessor>();
            var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

            try
            {
                var result = await processor.ProcessAsync(id);

                if (result.IsSuccess &&
                    result.Value?.Status == ImportBatchStatus.Cancelled)
                {
                    await auditLogService.LogAsync(
                        action: "ImportBackgroundCancelled",
                        entityType: "ImportBatch",
                        entityId: id,
                        userIdOverride: queuedByUserId);

                    return;
                }

                if (!result.IsSuccess &&
                    result.Error == "ImportBatchAlreadyCancelled")
                {
                    await auditLogService.LogAsync(
                        action: "ImportBackgroundCancelled",
                        entityType: "ImportBatch",
                        entityId: id,
                        userIdOverride: queuedByUserId);

                    return;
                }

                if (result.IsSuccess)
                {
                    await auditLogService.LogAsync(
                        action: "ImportBackgroundCompleted",
                        entityType: "ImportBatch",
                        entityId: id,
                        userIdOverride: queuedByUserId);
                }
                else
                {
                    await auditLogService.LogAsync(
                        action: "ImportBackgroundFailed",
                        entityType: "ImportBatch",
                        entityId: id,
                        userIdOverride: queuedByUserId);
                }
            }
            catch
            {
                await auditLogService.LogAsync(
                    action: "ImportBackgroundFailed",
                    entityType: "ImportBatch",
                    entityId: id,
                    userIdOverride: queuedByUserId);

                throw;
            }
        });

        var response = new
        {
            importBatchId = id,
            status = importBatch.Status.ToString()
        };

        return Accepted(ApiResponse<object>.Ok(response));
    }
}