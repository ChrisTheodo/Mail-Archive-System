using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/dashboard")]
public class AdminDashboardStorageUsageController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IStoragePathResolver _storagePathResolver;
    private readonly IAuditLogService _auditLogService;

    public AdminDashboardStorageUsageController(
        IMailArchiveDbContext db,
        IStoragePathResolver storagePathResolver,
        IAuditLogService auditLogService)
    {
        _db = db;
        _storagePathResolver = storagePathResolver;
        _auditLogService = auditLogService;
    }

    [HttpGet("storage-usage")]
    public async Task<IActionResult> GetStorageUsage()
    {
        var attachments = await _db.Attachments
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.StoragePath,
                x.SizeBytes
            })
            .ToListAsync();

        var importsWithPstFiles = await _db.ImportBatches
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.PstStoragePath))
            .Select(x => new
            {
                x.Id,
                x.PstStoragePath
            })
            .ToListAsync();

        var totalAttachmentBytesFromDatabase = attachments
            .Sum(x => (long)x.SizeBytes);

        var attachmentFilesFound = 0;
        var missingAttachmentFiles = 0;
        long attachmentStorageBytesOnDisk = 0;

        foreach (var attachment in attachments)
        {
            var fileSize = GetFileSizeOrZero(
                attachment.StoragePath,
                out var exists);

            if (exists)
            {
                attachmentFilesFound++;
                attachmentStorageBytesOnDisk += fileSize;
            }
            else
            {
                missingAttachmentFiles++;
            }
        }

        var pstFilesFound = 0;
        var missingPstFiles = 0;
        long pstStorageBytesOnDisk = 0;

        foreach (var importBatch in importsWithPstFiles)
        {
            var fileSize = GetFileSizeOrZero(
                importBatch.PstStoragePath,
                out var exists);

            if (exists)
            {
                pstFilesFound++;
                pstStorageBytesOnDisk += fileSize;
            }
            else
            {
                missingPstFiles++;
            }
        }

        var totalStorageBytesOnDisk =
            attachmentStorageBytesOnDisk +
            pstStorageBytesOnDisk;

        var storageHealthStatus =
            missingAttachmentFiles == 0 && missingPstFiles == 0
                ? "Healthy"
                : "MissingFilesDetected";

        var response = new AdminDashboardStorageUsageResponse(
            TotalAttachmentRecords: attachments.Count,
            TotalAttachmentBytesFromDatabase: totalAttachmentBytesFromDatabase,
            AttachmentFilesFound: attachmentFilesFound,
            MissingAttachmentFiles: missingAttachmentFiles,
            AttachmentStorageBytesOnDisk: attachmentStorageBytesOnDisk,
            ImportBatchesWithPstFile: importsWithPstFiles.Count,
            PstFilesFound: pstFilesFound,
            MissingPstFiles: missingPstFiles,
            PstStorageBytesOnDisk: pstStorageBytesOnDisk,
            TotalStorageBytesOnDisk: totalStorageBytesOnDisk,
            StorageHealthStatus: storageHealthStatus,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "AdminDashboardStorageUsageViewed",
            entityType: "Dashboard");

        return Ok(ApiResponse<AdminDashboardStorageUsageResponse>.Ok(response));
    }

    private long GetFileSizeOrZero(
        string? storagePath,
        out bool exists)
    {
        exists = false;

        if (string.IsNullOrWhiteSpace(storagePath))
            return 0;

        try
        {
            var resolvedPath = _storagePathResolver.ResolvePath(storagePath);

            if (!System.IO.File.Exists(resolvedPath))
                return 0;

            var fileInfo = new FileInfo(resolvedPath);

            exists = true;

            return fileInfo.Length;
        }
        catch
        {
            return 0;
        }
    }
}