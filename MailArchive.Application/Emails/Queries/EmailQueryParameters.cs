namespace MailArchive.Application.Emails.Queries;

public class EmailQueryParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Search { get; set; }

    public Guid? MailboxId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public string? Sender { get; set; }

    public string? Recipient { get; set; }

    public string? Subject { get; set; }

    public string? Folder { get; set; }

    public bool? HasAttachments { get; set; }

    public string? AttachmentFileName { get; set; }

    public string? SortBy { get; set; }

    public bool SortDescending { get; set; } = true;
}