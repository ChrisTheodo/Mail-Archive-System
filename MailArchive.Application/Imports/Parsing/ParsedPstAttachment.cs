namespace MailArchive.Application.Imports.Parsing;

public record ParsedPstAttachment(
    string FileName,
    string? ContentType,
    byte[] Content
);