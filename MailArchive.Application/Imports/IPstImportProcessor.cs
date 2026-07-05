using MailArchive.Application.Common;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Imports;

public interface IPstImportProcessor
{
    Task<Result<ImportBatch>> ProcessAsync(Guid importBatchId);
}