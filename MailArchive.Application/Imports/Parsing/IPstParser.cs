namespace MailArchive.Application.Imports.Parsing;

public interface IPstParser
{
    Task<IReadOnlyCollection<ParsedPstEmail>> ParseAsync(
        string pstFilePath,
        CancellationToken cancellationToken = default);
}