using System.Text.RegularExpressions;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Emails.Search;

public static class EmailSearchSnippet
{
    private const int MaxSnippetLength = 220;
    private const int ContextCharacters = 70;

    public static string? Create(Email email, string? search)
    {
        var terms = EmailSearchText.ExtractTerms(search);

        var candidates = new List<string?>
        {
            email.Subject,
            email.SenderEmail,
            email.SenderName,
            email.BodyText,
            StripHtml(email.BodyHtml),
            email.FolderPath,
            email.InternetMessageId
        };

        candidates.AddRange(email.Recipients.Select(x => x.RecipientEmail));
        candidates.AddRange(email.Recipients.Select(x => x.RecipientName));
        candidates.AddRange(email.Attachments.Select(x => x.FileName));

        if (terms.Count == 0)
        {
            return CreateFallbackSnippet(candidates);
        }

        foreach (var term in terms)
        {
            foreach (var candidate in candidates)
            {
                var snippet = CreateSnippetForTerm(candidate, term);

                if (!string.IsNullOrWhiteSpace(snippet))
                    return snippet;
            }
        }

        return CreateFallbackSnippet(candidates);
    }

    private static string? CreateSnippetForTerm(string? value, string term)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(term))
            return null;

        var normalized = NormalizeWhitespace(value);
        var index = normalized.IndexOf(term, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
            return null;

        var start = Math.Max(0, index - ContextCharacters);
        var availableLength = normalized.Length - start;
        var length = Math.Min(MaxSnippetLength, availableLength);

        var snippet = normalized.Substring(start, length).Trim();

        if (start > 0)
            snippet = "..." + snippet;

        if (start + length < normalized.Length)
            snippet += "...";

        return snippet;
    }

    private static string? CreateFallbackSnippet(IEnumerable<string?> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalized = NormalizeWhitespace(candidate);

            if (normalized.Length <= MaxSnippetLength)
                return normalized;

            return normalized[..MaxSnippetLength].Trim() + "...";
        }

        return null;
    }

    private static string? StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var withoutTags = Regex.Replace(
            value,
            "<.*?>",
            " ",
            RegexOptions.Singleline);

        return withoutTags;
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value, "\\s+", " ").Trim();
    }
}