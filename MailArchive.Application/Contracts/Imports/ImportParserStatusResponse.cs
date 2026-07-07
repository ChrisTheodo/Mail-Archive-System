namespace MailArchive.Application.Contracts.Imports;

public record ImportParserStatusResponse(
    string ActiveProvider,
    bool IsMock,
    bool IsXstReader
);