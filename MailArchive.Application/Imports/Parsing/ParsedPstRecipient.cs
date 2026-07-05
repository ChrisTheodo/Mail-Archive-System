using MailArchive.Domain.Enums;

namespace MailArchive.Application.Imports.Parsing;

public record ParsedPstRecipient(
    RecipientType RecipientType,
    string RecipientEmail,
    string? RecipientName
);