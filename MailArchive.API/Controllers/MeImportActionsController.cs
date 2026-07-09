using System.Security.Claims;
using System.Security.Cryptography;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using MailArchive.Application.Imports;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/imports")]
public class MeImportActionsController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MeImportActionsController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService,
        IServiceScopeFactory serviceScopeFactory)
    {
        _db = db;
        _auditLogService = auditLogService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    [HttpPost("pst/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1024L * 1024L * 1024L)]
    public async Task<IActionResult> UploadMyPstImport(
        [FromForm] Guid mailboxId,
        IFormFile file)
    {
        var currentUserIdResult = GetCurrentUserId();

        if (!currentUserIdResult.Success)
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserId = currentUserIdResult.UserId;

        var mailbox = await _db.Mailboxes
            .Include(x => x.OwnerUser)
            .FirstOrDefaultAsync(x =>
                x.Id == mailboxId &&
                x.OwnerUserId == currentUserId);

        if (mailbox == null)
            return NotFound(ApiResponse<string>.Fail("MailboxNotFound"));

        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("PstFileRequired"));

        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName))
            return BadRequest(ApiResponse<string>.Fail("PstFilenameRequired"));

        if (!originalFileName.EndsWith(".pst", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<string>.Fail("OnlyPstFilesAreAllowed"));

        var importsDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "storage",
            "imports");

        Directory.CreateDirectory(importsDirectory);

        var safeFileName = SanitizeFileName(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var absoluteStoragePath = Path.Combine(importsDirectory, storedFileName);

        await using (var outputStream = System.IO.File.Create(absoluteStoragePath))
        {
            await file.CopyToAsync(outputStream);
        }

        string pstHash;

        try
        {
            pstHash = await CalculateSha256Async(absoluteStoragePath);

            var duplicateExists = await _db.ImportBatches
                .AsNoTracking()
                .AnyAsync(x => x.PstHash == pstHash);

            if (duplicateExists)
            {
                TryDeleteFile(absoluteStoragePath);
                return Conflict(ApiResponse<string>.Fail("ImportBatchAlreadyExists"));
            }

            var relativeStoragePath = Path.Combine(
                    "storage",
                    "imports",
                    storedFileName)
                .Replace('\\', '/');

            var importBatch = new ImportBatch
            {
                Id = Guid.NewGuid(),
                PstFilename = originalFileName,
                PstHash = pstHash,
                PstStoragePath = relativeStoragePath,
                MailboxId = mailbox.Id,
                Status = ImportBatchStatus.Pending,
                StartedAt = DateTime.UtcNow,
                CompletedAt = null,
                TotalMessages = 0,
                ImportedMessages = 0,
                FailedMessages = 0
            };

            _db.ImportBatches.Add(importBatch);

            await _db.SaveChangesAsync();

            await _auditLogService.LogAsync(
                action: "MyPstImportUploaded",
                entityType: "ImportBatch",
                entityId: importBatch.Id);

            var response = new ImportBatchResponse(
                importBatch.Id,
                importBatch.PstFilename,
                importBatch.PstHash,
                importBatch.PstStoragePath,
                importBatch.MailboxId,
                mailbox.DisplayName,
                mailbox.OwnerUser?.Email,
                importBatch.Status.ToString(),
                importBatch.StartedAt,
                importBatch.CompletedAt,
                importBatch.TotalMessages,
                importBatch.ImportedMessages,
                importBatch.FailedMessages);

            return Ok(ApiResponse<ImportBatchResponse>.Ok(response));
        }
        catch
        {
            TryDeleteFile(absoluteStoragePath);
            throw;
        }
    }

    [HttpPost("{id:guid}/process/background")]
    public async Task<IActionResult> ProcessMyImportInBackground(Guid id)
    {
        var currentUserIdResult = GetCurrentUserId();

        if (!currentUserIdResult.Success)
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserId = currentUserIdResult.UserId;

        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Mailbox.OwnerUserId == currentUserId);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        if (importBatch.Status == ImportBatchStatus.Completed ||
            importBatch.Status == ImportBatchStatus.CompletedWithErrors)
        {
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCompleted"));
        }

        if (importBatch.Status == ImportBatchStatus.Failed)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyFailed"));

        if (importBatch.Status == ImportBatchStatus.Cancelled)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCancelled"));

        if (importBatch.Status == ImportBatchStatus.Queued)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyQueued"));

        if (importBatch.Status == ImportBatchStatus.Running)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyRunning"));

        importBatch.Status = ImportBatchStatus.Queued;

        await _db.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "MyImportProcessingQueued",
            entityType: "ImportBatch",
            entityId: importBatch.Id);

        _ = Task.Run(async () =>
        {
            using var scope = _serviceScopeFactory.CreateScope();

            var processor = scope.ServiceProvider.GetRequiredService<IPstImportProcessor>();
            var scopedDb = scope.ServiceProvider.GetRequiredService<IMailArchiveDbContext>();
            var scopedAuditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

            try
            {
                var result = await processor.ProcessAsync(id);

                var processedImport = await scopedDb.ImportBatches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (processedImport == null)
                    return;

                var action = processedImport.Status switch
                {
                    ImportBatchStatus.Completed => "MyImportBackgroundCompleted",
                    ImportBatchStatus.CompletedWithErrors => "MyImportBackgroundCompletedWithErrors",
                    ImportBatchStatus.Failed => "MyImportBackgroundFailed",
                    ImportBatchStatus.Cancelled => "MyImportBackgroundCancelled",
                    _ => result.IsSuccess
                        ? "MyImportBackgroundProcessed"
                        : "MyImportBackgroundFailed"
                };

                await scopedAuditLogService.LogAsync(
                    action: action,
                    entityType: "ImportBatch",
                    entityId: id);
            }
            catch
            {
                try
                {
                    var failedImport = await scopedDb.ImportBatches
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (failedImport != null)
                    {
                        failedImport.Status = ImportBatchStatus.Failed;
                        failedImport.CompletedAt = DateTime.UtcNow;
                        failedImport.FailedMessages = Math.Max(failedImport.FailedMessages, 1);

                        scopedDb.ImportErrors.Add(new ImportError
                        {
                            Id = Guid.NewGuid(),
                            ImportBatchId = failedImport.Id,
                            Message = "Unexpected background import failure.",
                            CreatedAt = DateTime.UtcNow
                        });

                        await scopedDb.SaveChangesAsync();

                        await scopedAuditLogService.LogAsync(
                            action: "MyImportBackgroundFailed",
                            entityType: "ImportBatch",
                            entityId: id);
                    }
                }
                catch
                {
                    // Last-resort protection: never crash the API process from a background task.
                }
            }
        });

        var response = new
        {
            importBatchId = importBatch.Id,
            status = importBatch.Status.ToString()
        };

        return Accepted(ApiResponse<object>.Ok(response));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelMyImport(Guid id)
    {
        var currentUserIdResult = GetCurrentUserId();

        if (!currentUserIdResult.Success)
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserId = currentUserIdResult.UserId;

        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Mailbox.OwnerUserId == currentUserId);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        if (importBatch.Status == ImportBatchStatus.Completed ||
            importBatch.Status == ImportBatchStatus.CompletedWithErrors)
        {
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCompleted"));
        }

        if (importBatch.Status == ImportBatchStatus.Failed)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyFailed"));

        if (importBatch.Status == ImportBatchStatus.Cancelled)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCancelled"));

        importBatch.Status = ImportBatchStatus.Cancelled;
        importBatch.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "MyImportCancelled",
            entityType: "ImportBatch",
            entityId: importBatch.Id);

        var response = new ImportBatchResponse(
            importBatch.Id,
            importBatch.PstFilename,
            importBatch.PstHash,
            importBatch.PstStoragePath,
            importBatch.MailboxId,
            importBatch.Mailbox.DisplayName,
            importBatch.Mailbox.OwnerUser?.Email,
            importBatch.Status.ToString(),
            importBatch.StartedAt,
            importBatch.CompletedAt,
            importBatch.TotalMessages,
            importBatch.ImportedMessages,
            importBatch.FailedMessages);

        return Ok(ApiResponse<ImportBatchResponse>.Ok(response));
    }

    private CurrentUserIdResult GetCurrentUserId()
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdText, out var currentUserId))
            return new CurrentUserIdResult(false, Guid.Empty);

        return new CurrentUserIdResult(true, currentUserId);
    }

    private static async Task<string> CalculateSha256Async(string filePath)
    {
        await using var stream = System.IO.File.OpenRead(filePath);

        var hashBytes = await SHA256.HashDataAsync(stream);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var sanitized = new string(
            fileName
                .Select(character => invalidChars.Contains(character) ? '_' : character)
                .ToArray());

        sanitized = sanitized.Trim();

        return string.IsNullOrWhiteSpace(sanitized)
            ? $"import-{Guid.NewGuid():N}.pst"
            : sanitized;
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }

    private readonly record struct CurrentUserIdResult(
        bool Success,
        Guid UserId);
}