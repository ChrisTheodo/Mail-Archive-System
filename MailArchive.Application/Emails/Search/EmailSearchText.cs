using System.Text.RegularExpressions;

namespace MailArchive.Application.Emails.Search;

public static class EmailSearchText
{
    private const int MaxTerms = 8;
    private const int MaxTermLength = 100;

    public static IReadOnlyCollection<string> ExtractTerms(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return Array.Empty<string>();

        var matches = Regex.Matches(
            search.Trim(),
            "\"[^\"]+\"|\\S+",
            RegexOptions.Compiled);

        return matches
            .Select(x => x.Value.Trim().Trim('"').Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Length > MaxTermLength
                ? x[..MaxTermLength]
                : x)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTerms)
            .ToList();
    }

    public static string ToContainsPattern(string value)
    {
        return $"%{EscapeLikePattern(value.Trim())}%";
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}