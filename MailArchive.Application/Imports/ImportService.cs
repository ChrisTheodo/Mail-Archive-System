using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using MailArchive.Application.Imports.Queries;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Imports;

public class ImportService : IImportService
{
    private readonly IMailArchiveDbContext _db;
    private readonly IPstImportProcessor _pstImportProcessor;

    public ImportService(
        IMailArchiveDbContext db,
        IPstImportProcessor pstImportProcessor)
    {
        _db = db;
        _pstImportProcessor = pstImportProcessor;
    }

    public async Task<PagedResult<ImportBatch>> GetPagedAsync(ImportBatchQueryParameters query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var baseQuery = _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .AsQueryable();

        if (query.MailboxId.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.MailboxId == query.MailboxId.Value);
        }

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.PstFilename.ToLower().Contains(search) ||
                x.PstHash.ToLower().Contains(search) ||
                (x.PstStoragePath != null && x.PstStoragePath.ToLower().Contains(search)) ||
                x.Mailbox.DisplayName.ToLower().Contains(search) ||
                x.Mailbox.OwnerUser.Email.ToLower().Contains(search));
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.StartedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.StartedAt <= query.ToDate.Value);
        }

        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ImportBatch>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result<ImportBatch>> GetByIdAsync(Guid id)
    {
        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return Result<ImportBatch>.Failure("ImportBatchNotFound");

        return Result<ImportBatch>.Success(importBatch);
    }

    public async Task<Result<ImportBatch>> CreatePstImportAsync(CreatePstImportRequest request)
    {
        var pstFilename = request.PstFilename.Trim();
        var pstHash = request.PstHash.Trim();
        var pstStoragePath = string.IsNullOrWhiteSpace(request.PstStoragePath)
            ? null
            : request.PstStoragePath.Trim();

        if (string.IsNullOrWhiteSpace(pstFilename))
            return Result<ImportBatch>.Failure("PstFilenameRequired");

        if (string.IsNullOrWhiteSpace(pstHash))
            return Result<ImportBatch>.Failure("PstHashRequired");

        var mailbox = await _db.Mailboxes
            .Include(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == request.MailboxId);

        if (mailbox == null)
            return Result<ImportBatch>.Failure("MailboxNotFound");

        var duplicateExists = await _db.ImportBatches.AnyAsync(x =>
            x.MailboxId == request.MailboxId &&
            x.PstHash == pstHash);

        if (duplicateExists)
            return Result<ImportBatch>.Failure("ImportBatchAlreadyExists");

        var importBatch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            MailboxId = request.MailboxId,
            PstFilename = pstFilename,
            PstHash = pstHash,
            PstStoragePath = pstStoragePath,
            Status = ImportBatchStatus.Pending,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            TotalMessages = 0,
            ImportedMessages = 0,
            FailedMessages = 0
        };

        _db.ImportBatches.Add(importBatch);
        await _db.SaveChangesAsync();

        var created = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstAsync(x => x.Id == importBatch.Id);

        return Result<ImportBatch>.Success(created);
    }

    public async Task<Result<ImportBatch>> StartAsync(Guid id)
    {
        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return Result<ImportBatch>.Failure("ImportBatchNotFound");

        if (importBatch.Status == ImportBatchStatus.Running)
            return Result<ImportBatch>.Failure("ImportBatchAlreadyRunning");

        if (importBatch.Status == ImportBatchStatus.Completed)
            return Result<ImportBatch>.Failure("ImportBatchAlreadyCompleted");

        importBatch.Status = ImportBatchStatus.Running;
        importBatch.StartedAt = DateTime.UtcNow;
        importBatch.CompletedAt = null;

        await _db.SaveChangesAsync();

        return Result<ImportBatch>.Success(importBatch);
    }

    public async Task<Result<ImportBatch>> CompleteAsync(Guid id, CompleteImportRequest request)
    {
        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return Result<ImportBatch>.Failure("ImportBatchNotFound");

        if (request.TotalMessages < 0 ||
            request.ImportedMessages < 0 ||
            request.FailedMessages < 0)
        {
            return Result<ImportBatch>.Failure("InvalidImportCounters");
        }

        importBatch.TotalMessages = request.TotalMessages;
        importBatch.ImportedMessages = request.ImportedMessages;
        importBatch.FailedMessages = request.FailedMessages;
        importBatch.CompletedAt = DateTime.UtcNow;
        importBatch.Status = request.FailedMessages > 0
            ? ImportBatchStatus.CompletedWithErrors
            : ImportBatchStatus.Completed;

        await _db.SaveChangesAsync();

        return Result<ImportBatch>.Success(importBatch);
    }

    public async Task<Result<ImportBatch>> FailAsync(Guid id, FailImportRequest request)
    {
        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return Result<ImportBatch>.Failure("ImportBatchNotFound");

        if (request.TotalMessages < 0 ||
            request.ImportedMessages < 0 ||
            request.FailedMessages < 0)
        {
            return Result<ImportBatch>.Failure("InvalidImportCounters");
        }

        importBatch.TotalMessages = request.TotalMessages;
        importBatch.ImportedMessages = request.ImportedMessages;
        importBatch.FailedMessages = request.FailedMessages;
        importBatch.CompletedAt = DateTime.UtcNow;
        importBatch.Status = ImportBatchStatus.Failed;

        await _db.SaveChangesAsync();

        return Result<ImportBatch>.Success(importBatch);
    }

    public async Task<Result<ImportBatch>> ProcessAsync(Guid id)
    {
        return await _pstImportProcessor.ProcessAsync(id);
    }
}