using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using MailArchive.Application.Imports.Queries;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Imports;

public interface IImportService
{
    Task<PagedResult<ImportBatch>> GetPagedAsync(ImportBatchQueryParameters query);

    Task<Result<ImportBatch>> GetByIdAsync(Guid id);

    Task<Result<ImportBatch>> CreatePstImportAsync(CreatePstImportRequest request);

    Task<Result<ImportBatch>> StartAsync(Guid id);

    Task<Result<ImportBatch>> CompleteAsync(Guid id, CompleteImportRequest request);

    Task<Result<ImportBatch>> FailAsync(Guid id, FailImportRequest request);
}