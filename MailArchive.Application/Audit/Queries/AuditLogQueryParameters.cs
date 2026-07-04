namespace MailArchive.Application.Audit.Queries;

public class AuditLogQueryParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Search { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public Guid? UserId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}