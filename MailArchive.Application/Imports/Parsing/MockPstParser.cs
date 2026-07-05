using MailArchive.Domain.Enums;

namespace MailArchive.Application.Imports.Parsing;

public class MockPstParser : IPstParser
{
    public Task<IReadOnlyCollection<ParsedPstEmail>> ParseAsync(
        string pstFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pstFilePath))
            throw new FileNotFoundException("PST file was not found.", pstFilePath);

        var fileInfo = new FileInfo(pstFilePath);
        var fileName = fileInfo.Name;
        var now = DateTime.UtcNow;

        IReadOnlyCollection<ParsedPstEmail> emails = new List<ParsedPstEmail>
        {
            new ParsedPstEmail(
                InternetMessageId: $"<mock-parser-{Guid.NewGuid():N}-001@mailarchive.local>",
                FolderPath: "Inbox",
                SenderEmail: "mock.parser@example.com",
                SenderName: "Mock PST Parser",
                Subject: $"Parsed mock email 1 from {fileName}",
                BodyText: $"This email was produced by the parser abstraction from file {fileName}. File size: {fileInfo.Length} bytes.",
                BodyHtml: $"<p>This email was produced by the parser abstraction from file {fileName}. File size: {fileInfo.Length} bytes.</p>",
                SentAt: now.AddMinutes(-45),
                ReceivedAt: now.AddMinutes(-44),
                Recipients: new List<ParsedPstRecipient>
                {
                    new ParsedPstRecipient(
                        RecipientType.To,
                        "user@example.com",
                        "Test User")
                }
            ),
            new ParsedPstEmail(
                InternetMessageId: $"<mock-parser-{Guid.NewGuid():N}-002@mailarchive.local>",
                FolderPath: "Inbox",
                SenderEmail: "mock.parser.notifications@example.com",
                SenderName: "Mock PST Parser Notifications",
                Subject: $"Parsed mock email 2 from {fileName}",
                BodyText: $"This is the second email produced by the parser abstraction from file {fileName}. File size: {fileInfo.Length} bytes.",
                BodyHtml: $"<p>This is the second email produced by the parser abstraction from file {fileName}. File size: {fileInfo.Length} bytes.</p>",
                SentAt: now.AddMinutes(-30),
                ReceivedAt: now.AddMinutes(-29),
                Recipients: new List<ParsedPstRecipient>
                {
                    new ParsedPstRecipient(
                        RecipientType.To,
                        "user@example.com",
                        "Test User")
                }
            )
        };

        return Task.FromResult(emails);
    }
}