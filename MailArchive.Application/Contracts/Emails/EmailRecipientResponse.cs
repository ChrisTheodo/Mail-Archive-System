namespace MailArchive.Application.Contracts.Emails;

public record EmailRecipientResponse(
    Guid Id,
    string RecipientType,
    string RecipientEmail,
    string? RecipientName
);