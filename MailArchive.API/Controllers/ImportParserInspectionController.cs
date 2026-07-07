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
public class ImportParserInspectionController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IPstParser _pstParser;
    private readonly IStoragePathResolver _storagePathResolver;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogService _auditLogService;

    public ImportParserInspectionController(
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

    [HttpGet("{id:guid}/parser/inspect")]
    public async Task<IActionResult> InspectImportParser(Guid id)
    {
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
                action: "ImportParserInspectionFailed",
                entityType: "ImportBatch",
                entityId: id);

            return BadRequest(ApiResponse<string>.Fail(
                $"ImportParserInspectionFailed: {ex.Message}"));
        }

        var folders = parsedEmails
            .Select(x => x.FolderPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var emailDates = parsedEmails
            .Select(x => x.SentAt ?? x.ReceivedAt)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        var activeParserProvider = _configuration["MailArchive:PstParserProvider"];

        if (string.IsNullOrWhiteSpace(activeParserProvider))
            activeParserProvider = "Mock";

        var response = new ImportParserInspectionResponse(
            ImportBatchId: importBatch.Id,
            PstFilename: importBatch.PstFilename,
            ActiveParserProvider: activeParserProvider,
            TotalEmails: parsedEmails.Count,
            EmailsWithAttachments: parsedEmails.Count(x => x.Attachments.Count > 0),
            TotalAttachments: parsedEmails.Sum(x => x.Attachments.Count),
            TotalRecipients: parsedEmails.Sum(x => x.Recipients.Count),
            FolderCount: folders.Count,
            Folders: folders,
            EarliestEmailDate: emailDates.Count == 0 ? null : emailDates.Min(),
            LatestEmailDate: emailDates.Count == 0 ? null : emailDates.Max()
        );

        await _auditLogService.LogAsync(
            action: "ImportParserInspected",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<ImportParserInspectionResponse>.Ok(response));
    }
}