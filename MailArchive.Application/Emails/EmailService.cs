using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Application.Emails.Queries;
using MailArchive.Application.Emails.Search;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Emails;

public class EmailService : IEmailService
{
    private const int MaxPageSize = 100;

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
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var searchTerms = EmailSearchText.ExtractTerms(query.Search);

        var emailsQuery = _db.Emails
            .AsNoTracking()
            .Include(x => x.Mailbox)
                .ThenInclude(x => x.OwnerUser)
            .Include(x => x.Recipients)
            .Include(x => x.Attachments)
            .AsQueryable();

        emailsQuery = ApplyUserIsolation(emailsQuery);
        emailsQuery = ApplyFilters(emailsQuery, query);
        emailsQuery = ApplySearch(emailsQuery, searchTerms);

        var totalCount = await emailsQuery.CountAsync();

        emailsQuery = ApplySorting(emailsQuery, query, searchTerms);

        var items = await emailsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Email>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result<Email>> GetByIdAsync(Guid id)
    {
        var query = _db.Emails
            .AsNoTracking()
            .Include(x => x.Mailbox)
                .ThenInclude(x => x.OwnerUser)
            .Include(x => x.Recipients)
            .Include(x => x.Attachments)
            .Where(x => x.Id == id);

        query = ApplyUserIsolation(query);

        var email = await query.FirstOrDefaultAsync();

        if (email == null)
            return Result<Email>.Failure("EmailNotFound");

        return Result<Email>.Success(email);
    }

    public async Task<Result<List<Attachment>>> GetAttachmentsByEmailIdAsync(Guid emailId)
    {
        var emailQuery = _db.Emails
            .AsNoTracking()
            .Include(x => x.Mailbox)
                .ThenInclude(x => x.OwnerUser)
            .Where(x => x.Id == emailId);

        emailQuery = ApplyUserIsolation(emailQuery);

        var emailExists = await emailQuery.AnyAsync();

        if (!emailExists)
            return Result<List<Attachment>>.Failure("EmailNotFound");

        var attachments = await _db.Attachments
            .AsNoTracking()
            .Where(x => x.EmailId == emailId)
            .OrderBy(x => x.FileName)
            .ToListAsync();

        return Result<List<Attachment>>.Success(attachments);
    }

    private IQueryable<Email> ApplyUserIsolation(IQueryable<Email> query)
    {
        if (_currentUser.IsAdmin)
            return query;

        if (!_currentUser.UserId.HasValue)
            return query.Where(x => false);

        return query.Where(x => x.Mailbox.OwnerUserId == _currentUser.UserId.Value);
    }

    private static IQueryable<Email> ApplyFilters(
        IQueryable<Email> query,
        EmailQueryParameters parameters)
    {
        if (parameters.MailboxId.HasValue)
        {
            query = query.Where(x => x.MailboxId == parameters.MailboxId.Value);
        }

        if (parameters.FromDate.HasValue)
        {
            query = query.Where(x =>
                (x.ReceivedAt ?? x.SentAt ?? x.CreatedAt) >= parameters.FromDate.Value);
        }

        if (parameters.ToDate.HasValue)
        {
            query = query.Where(x =>
                (x.ReceivedAt ?? x.SentAt ?? x.CreatedAt) <= parameters.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sender))
        {
            var pattern = EmailSearchText.ToContainsPattern(parameters.Sender);

            query = query.Where(x =>
                EF.Functions.ILike(x.SenderEmail ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.SenderName ?? string.Empty, pattern));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Recipient))
        {
            var pattern = EmailSearchText.ToContainsPattern(parameters.Recipient);

            query = query.Where(x =>
                x.Recipients.Any(r =>
                    EF.Functions.ILike(r.RecipientEmail ?? string.Empty, pattern) ||
                    EF.Functions.ILike(r.RecipientName ?? string.Empty, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Subject))
        {
            var pattern = EmailSearchText.ToContainsPattern(parameters.Subject);

            query = query.Where(x =>
                EF.Functions.ILike(x.Subject ?? string.Empty, pattern));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Folder))
        {
            var pattern = EmailSearchText.ToContainsPattern(parameters.Folder);

            query = query.Where(x =>
                EF.Functions.ILike(x.FolderPath ?? string.Empty, pattern));
        }

        if (parameters.HasAttachments.HasValue)
        {
            query = query.Where(x => x.HasAttachments == parameters.HasAttachments.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.AttachmentFileName))
        {
            var pattern = EmailSearchText.ToContainsPattern(parameters.AttachmentFileName);

            query = query.Where(x =>
                x.Attachments.Any(a =>
                    EF.Functions.ILike(a.FileName ?? string.Empty, pattern)));
        }

        return query;
    }

    private static IQueryable<Email> ApplySearch(
        IQueryable<Email> query,
        IReadOnlyCollection<string> searchTerms)
    {
        foreach (var term in searchTerms)
        {
            var pattern = EmailSearchText.ToContainsPattern(term);

            query = query.Where(x =>
                EF.Functions.ILike(x.Subject ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.BodyText ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.BodyHtml ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.SenderEmail ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.SenderName ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.InternetMessageId ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.FolderPath ?? string.Empty, pattern) ||
                x.Recipients.Any(r =>
                    EF.Functions.ILike(r.RecipientEmail ?? string.Empty, pattern) ||
                    EF.Functions.ILike(r.RecipientName ?? string.Empty, pattern)) ||
                x.Attachments.Any(a =>
                    EF.Functions.ILike(a.FileName ?? string.Empty, pattern)));
        }

        return query;
    }

    private static IQueryable<Email> ApplySorting(
        IQueryable<Email> query,
        EmailQueryParameters parameters,
        IReadOnlyCollection<string> searchTerms)
    {
        var sortBy = parameters.SortBy?.Trim().ToLowerInvariant();
        var sortDescending = parameters.SortDescending;

        if (string.IsNullOrWhiteSpace(sortBy) && searchTerms.Count > 0)
        {
            var firstTerm = searchTerms.First();
            var pattern = EmailSearchText.ToContainsPattern(firstTerm);

            return query
                .OrderByDescending(x => EF.Functions.ILike(x.Subject ?? string.Empty, pattern))
                .ThenByDescending(x => EF.Functions.ILike(x.SenderEmail ?? string.Empty, pattern))
                .ThenByDescending(x => x.ReceivedAt ?? x.SentAt ?? x.CreatedAt);
        }

        return sortBy switch
        {
            "subject" => sortDescending
                ? query.OrderByDescending(x => x.Subject)
                    .ThenByDescending(x => x.ReceivedAt ?? x.SentAt ?? x.CreatedAt)
                : query.OrderBy(x => x.Subject)
                    .ThenByDescending(x => x.ReceivedAt ?? x.SentAt ?? x.CreatedAt),

            "sender" or "senderemail" => sortDescending
                ? query.OrderByDescending(x => x.SenderEmail)
                    .ThenByDescending(x => x.ReceivedAt ?? x.SentAt ?? x.CreatedAt)
                : query.OrderBy(x => x.SenderEmail)
                    .ThenByDescending(x => x.ReceivedAt ?? x.SentAt ?? x.CreatedAt),

            "sentat" or "sent" => sortDescending
                ? query.OrderByDescending(x => x.SentAt)
                    .ThenByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.SentAt)
                    .ThenByDescending(x => x.CreatedAt),

            "createdat" or "created" => sortDescending
                ? query.OrderByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.CreatedAt),

            "receivedat" or "received" or _ => sortDescending
                ? query.OrderByDescending(x => x.ReceivedAt ?? x.SentAt ?? x.CreatedAt)
                    .ThenByDescending(x => x.CreatedAt)
                : query.OrderBy(x => x.ReceivedAt ?? x.SentAt ?? x.CreatedAt)
                    .ThenByDescending(x => x.CreatedAt)
        };
    }
}