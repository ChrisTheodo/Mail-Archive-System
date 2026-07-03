using MailArchive.Application.Common;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Attachments;

public interface IAttachmentService
{
    Task<Result<Attachment>> GetByIdAsync(Guid id);
}