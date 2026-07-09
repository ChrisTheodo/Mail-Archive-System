namespace MailArchive.Application.Me.Queries;

public class MyImportQueryParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public Guid? MailboxId { get; set; }

    public string? Status { get; set; }
}