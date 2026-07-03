using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Application.Emails.Queries;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Emails;

public class EmailService : IEmailService
{
    private readonly IMailArchiveDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public EmailService(
        IMailArchiveDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<Email>> GetPagedAsync(EmailQueryParameters query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var baseQuery = _db.Emails
            .Include(x => x.Mailbox)
            .Include(x => x.Recipients)
            .Include(x => x.Attachments)
            .AsQueryable();

        baseQuery = ApplyUserIsolation(baseQuery);

        if (query.MailboxId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.MailboxId == query.MailboxId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                (x.Subject != null && x.Subject.ToLower().Contains(search)) ||
                (x.BodyText != null && x.BodyText.ToLower().Contains(search)) ||
                (x.SenderEmail != null && x.SenderEmail.ToLower().Contains(search)) ||
                x.Recipients.Any(r => r.RecipientEmail.ToLower().Contains(search)) ||
                x.Attachments.Any(a => a.FileName.ToLower().Contains(search)));
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.ReceivedAt >= query.FromDate.Value ||
                x.SentAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.ReceivedAt <= query.ToDate.Value ||
                x.SentAt <= query.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Sender))
        {
            var sender = query.Sender.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.SenderEmail != null &&
                x.SenderEmail.ToLower().Contains(sender));
        }

        if (!string.IsNullOrWhiteSpace(query.Recipient))
        {
            var recipient = query.Recipient.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.Recipients.Any(r =>
                    r.RecipientEmail.ToLower().Contains(recipient)));
        }

        if (!string.IsNullOrWhiteSpace(query.Subject))
        {
            var subject = query.Subject.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.Subject != null &&
                x.Subject.ToLower().Contains(subject));
        }

        if (!string.IsNullOrWhiteSpace(query.Folder))
        {
            var folder = query.Folder.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.FolderPath != null &&
                x.FolderPath.ToLower().Contains(folder));
        }

        if (query.HasAttachments.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.HasAttachments == query.HasAttachments.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.AttachmentFileName))
        {
            var fileName = query.AttachmentFileName.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.Attachments.Any(a =>
                    a.FileName.ToLower().Contains(fileName)));
        }

        var total = await baseQuery.CountAsync();

        var orderedQuery = ApplySorting(baseQuery, query.SortBy, query.SortDescending);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Email>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result<Email>> GetByIdAsync(Guid id)
    {
        var query = _db.Emails
            .Include(x => x.Mailbox)
            .Include(x => x.Recipients)
            .Include(x => x.Attachments)
            .Where(x => x.Id == id)
            .AsQueryable();

        query = ApplyUserIsolation(query);

        var email = await query.FirstOrDefaultAsync();

        if (email == null)
            return Result<Email>.Failure("EmailNotFound");

        return Result<Email>.Success(email);
    }

    public async Task<Result<List<Attachment>>> GetAttachmentsByEmailIdAsync(Guid emailId)
    {
        var emailQuery = _db.Emails
            .Include(x => x.Mailbox)
            .Where(x => x.Id == emailId)
            .AsQueryable();

        emailQuery = ApplyUserIsolation(emailQuery);

        var emailExists = await emailQuery.AnyAsync();

        if (!emailExists)
            return Result<List<Attachment>>.Failure("EmailNotFound");

        var attachments = await _db.Attachments
            .Where(x => x.EmailId == emailId)
            .OrderBy(x => x.FileName)
            .ToListAsync();

        return Result<List<Attachment>>.Success(attachments);
    }

    private IQueryable<Email> ApplyUserIsolation(IQueryable<Email> query)
    {
        if (_currentUser.IsAdmin)
            return query;

        var currentUserId = _currentUser.UserId;

        if (!currentUserId.HasValue)
            return query.Where(x => false);

        return query.Where(x => x.Mailbox.OwnerUserId == currentUserId.Value);
    }

    private static IQueryable<Email> ApplySorting(
        IQueryable<Email> query,
        string? sortBy,
        bool sortDescending)
    {
        var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();

        return normalizedSortBy switch
        {
            "subject" => sortDescending
                ? query.OrderByDescending(x => x.Subject)
                : query.OrderBy(x => x.Subject),

            "sender" => sortDescending
                ? query.OrderByDescending(x => x.SenderEmail)
                : query.OrderBy(x => x.SenderEmail),

            "sentat" => sortDescending
                ? query.OrderByDescending(x => x.SentAt)
                : query.OrderBy(x => x.SentAt),

            "createdat" => sortDescending
                ? query.OrderByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.CreatedAt),

            _ => sortDescending
                ? query.OrderByDescending(x => x.ReceivedAt)
                : query.OrderBy(x => x.ReceivedAt)
        };
    }
}