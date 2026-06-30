using MailArchive.Domain.Enums;

namespace MailArchive.Domain.Entities;

public class EmailRecipient
{
    public Guid Id { get; set; }

    public Guid EmailId { get; set; }
    public Email Email { get; set; } = null!;

    public RecipientType RecipientType { get; set; }

    public string RecipientEmail { get; set; } = null!;
    public string? RecipientName { get; set; }
}