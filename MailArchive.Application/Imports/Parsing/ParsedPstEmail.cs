namespace MailArchive.Application.Imports.Parsing;

public record ParsedPstEmail(
    string? InternetMessageId,
    string FolderPath,
    string SenderEmail,
    string? SenderName,
    string? Subject,
    string? BodyText,
    string? BodyHtml,
    DateTime? SentAt,
    DateTime? ReceivedAt,
    IReadOnlyCollection<ParsedPstRecipient> Recipients
);