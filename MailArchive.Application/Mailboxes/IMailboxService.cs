using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Mailboxes;
using MailArchive.Application.Mailboxes.Queries;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Mailboxes;

public interface IMailboxService
{
    Task<Result<List<Mailbox>>> GetAllAsync();
    Task<Result<Mailbox>> GetByIdAsync(Guid id);
    Task<Result<Mailbox>> CreateAsync(CreateMailboxRequest request);
    Task<Result<Mailbox>> UpdateAsync(Guid id, UpdateMailboxRequest request);

    Task<PagedResult<Mailbox>> GetPagedAsync(MailboxQueryParameters query);
}