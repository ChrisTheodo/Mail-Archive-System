using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using MailArchive.Application.Imports.Parsing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/imports")]
public class ImportParserPreviewController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IPstParser _pstParser;
    private readonly IStoragePathResolver _storagePathResolver;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogService _auditLogService;

    public ImportParserPreviewController(
        IMailArchiveDbContext db,
        IPstParser pstParser,
        IStoragePathResolver storagePathResolver,
        IConfiguration configuration,
        IAuditLogService auditLogService)
    {
        _db = db;
        _pstParser = pstParser;
        _storagePathResolver = storagePathResolver;
        _configuration = configuration;
        _auditLogService = auditLogService;
    }

    [HttpGet("{id:guid}/parser/preview")]
    public async Task<IActionResult> PreviewImportParser(
        Guid id,
        [FromQuery] int take = 10)
    {
        take = Math.Clamp(take, 1, 50);

        var importBatch = await _db.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        if (string.IsNullOrWhiteSpace(importBatch.PstStoragePath))
            return BadRequest(ApiResponse<string>.Fail("PstStoragePathMissing"));

        var pstFilePath = _storagePathResolver.ResolvePath(importBatch.PstStoragePath);

        if (!System.IO.File.Exists(pstFilePath))
            return BadRequest(ApiResponse<string>.Fail("PstFileNotFound"));

        IReadOnlyCollection<ParsedPstEmail> parsedEmails;

        try
        {
            parsedEmails = await _pstParser.ParseAsync(
                pstFilePath,
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await _auditLogService.LogAsync(
                action: "ImportParserPreviewFailed",
                entityType: "ImportBatch",
                entityId: id);

            return BadRequest(ApiResponse<string>.Fail(
                $"ImportParserPreviewFailed: {ex.Message}"));
        }

        var activeParserProvider = _configuration["MailArchive:PstParserProvider"];

        if (string.IsNullOrWhiteSpace(activeParserProvider))
            activeParserProvider = "Mock";

        var previewEmails = parsedEmails
            .Take(take)
            .Select((email, index) => new ImportParserPreviewEmailResponse(
                Index: index + 1,
                InternetMessageId: email.InternetMessageId,
                FolderPath: email.FolderPath,
                SenderEmail: string.IsNullOrWhiteSpace(email.SenderEmail)
                    ? "unknown@unknown.local"
                    : email.SenderEmail,
                SenderName: email.SenderName,
                Subject: email.Subject,
                SentAt: email.SentAt,
                ReceivedAt: email.ReceivedAt,
                RecipientCount: email.Recipients.Count,
                AttachmentCount: email.Attachments.Count,
                RecipientEmails: email.Recipients
                    .Select(x => x.RecipientEmail)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList(),
                AttachmentFileNames: email.Attachments
                    .Select(x => x.FileName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList(),
                BodyPreview: CreateBodyPreview(email.BodyText ?? email.BodyHtml)
            ))
            .ToList();

        var response = new ImportParserPreviewResponse(
            ImportBatchId: importBatch.Id,
            PstFilename: importBatch.PstFilename,
            ActiveParserProvider: activeParserProvider,
            TotalParsedEmails: parsedEmails.Count,
            ReturnedEmails: previewEmails.Count,
            Emails: previewEmails
        );

        await _auditLogService.LogAsync(
            action: "ImportParserPreviewed",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<ImportParserPreviewResponse>.Ok(response));
    }

    private static string? CreateBodyPreview(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var normalized = string.Join(
            " ",
            body.Split(
                new[] { ' ', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length <= 250)
            return normalized;

        return normalized[..250] + "...";
    }
}