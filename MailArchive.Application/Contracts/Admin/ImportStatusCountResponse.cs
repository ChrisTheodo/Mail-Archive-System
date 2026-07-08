namespace MailArchive.Application.Contracts.Admin;

public record ImportStatusCountResponse(
    string Status,
    int Count
);