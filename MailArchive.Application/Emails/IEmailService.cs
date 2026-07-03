using MailArchive.Application.Common;
using MailArchive.Application.Emails.Queries;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Emails;

public interface IEmailService
{
    Task<PagedResult<Email>> GetPagedAsync(EmailQueryParameters query);

    Task<Result<Email>> GetByIdAsync(Guid id);

    Task<Result<List<Attachment>>> GetAttachmentsByEmailIdAsync(Guid emailId);
}