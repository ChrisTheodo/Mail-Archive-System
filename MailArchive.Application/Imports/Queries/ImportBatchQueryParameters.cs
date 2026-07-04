using MailArchive.Domain.Enums;

namespace MailArchive.Application.Imports.Queries;

public class ImportBatchQueryParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Search { get; set; }

    public Guid? MailboxId { get; set; }

    public ImportBatchStatus? Status { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}