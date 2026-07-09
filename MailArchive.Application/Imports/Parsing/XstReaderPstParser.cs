using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using MailArchive.Domain.Enums;
using XstReader;
using System.Text;

namespace MailArchive.Application.Imports.Parsing;

public class XstReaderPstParser : IPstParser
{
    public Task<IReadOnlyCollection<ParsedPstEmail>> ParseAsync(
        string pstFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pstFilePath))
            throw new ArgumentException("PST file path is required.", nameof(pstFilePath));

        if (!File.Exists(pstFilePath))
            throw new FileNotFoundException("PST file was not found.", pstFilePath);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var parsedEmails = new List<ParsedPstEmail>();

            // Important:
            // Do not use "using var" here and do not call ClearContents().
            // XstReader can throw unhandled exceptions during internal cleanup for some PST files.
            // We prefer stable parsing over aggressive cleanup inside the API process.
            var xstFile = new XstFile(pstFilePath);

            TraverseFolder(
                folder: xstFile.RootFolder,
                parentPath: string.Empty,
                parsedEmails: parsedEmails,
                cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyCollection<ParsedPstEmail>>(parsedEmails);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"XstReaderParsingFailed: {GetInnermostMessage(ex)}", ex);
        }
    }

    private static void TraverseFolder(
        object folder,
        string parentPath,
        List<ParsedPstEmail> parsedEmails,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folderName =
            GetString(folder, "Name") ??
            GetString(folder, "DisplayName") ??
            "Folder";

        var folderPath = string.IsNullOrWhiteSpace(parentPath)
            ? folderName
            : $"{parentPath}/{folderName}";

        foreach (var message in GetEnumerable(folder, "Messages"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var parsedEmail = MapMessage(message, folderPath);
                parsedEmails.Add(parsedEmail);
            }
            catch
            {
                // Keep parser stable. Individual malformed messages should not crash the whole PST parsing.
                // Later we can extend ParsedPstEmail/ParseResult to return parse warnings explicitly.
            }
        }

        foreach (var childFolder in GetEnumerable(folder, "Folders"))
        {
            TraverseFolder(
                folder: childFolder,
                parentPath: folderPath,
                parsedEmails: parsedEmails,
                cancellationToken: cancellationToken);
        }

        // Do not call ClearContents().
        // XstReader may start internal cleanup that can throw outside our call stack.
    }

    private static ParsedPstEmail MapMessage(object message, string folderPath)
    {
        var recipientsObject = GetObject(message, "Recipients");

        var senderObject =
            GetObject(recipientsObject, "Sender") ??
            GetObject(recipientsObject, "Originator") ??
            GetObject(recipientsObject, "SentRepresenting") ??
            GetObject(message, "Sender");

        var senderEmail =
            NormalizeEmail(GetRecipientEmail(senderObject)) ??
            NormalizeEmail(GetString(message, "SenderEmail")) ??
            NormalizeEmail(GetString(message, "SenderEmailAddress")) ??
            NormalizeEmail(GetString(message, "FromEmail")) ??
            NormalizeEmail(GetStringFromProperties(message, "SenderEmailAddress")) ??
            NormalizeEmail(GetStringFromProperties(message, "SenderEmail")) ??
            "unknown@unknown.local";

        var senderName =
            GetRecipientName(senderObject) ??
            GetString(message, "SenderName") ??
            GetString(message, "FromName") ??
            GetStringFromProperties(message, "SenderName");

        var subject =
            GetString(message, "Subject") ??
            GetStringFromProperties(message, "Subject") ??
            "(No subject)";

        var internetMessageId =
            GetString(message, "InternetMessageId") ??
            GetString(message, "InternetMessageID") ??
            GetStringFromProperties(message, "InternetMessageId") ??
            GetStringFromProperties(message, "InternetMessageID") ??
            $"<{Guid.NewGuid():N}@xstreader.local>";

        var sentAt =
            GetDateTime(message, "SentAt") ??
            GetDateTime(message, "SubmitTime") ??
            GetDateTime(message, "ClientSubmitTime") ??
            GetDateTime(message, "DeliveryTime") ??
            GetDateTimeFromProperties(message, "ClientSubmitTime") ??
            GetDateTimeFromProperties(message, "DeliveryTime");

        var receivedAt =
            GetDateTime(message, "ReceivedAt") ??
            GetDateTime(message, "DeliveryTime") ??
            GetDateTimeFromProperties(message, "DeliveryTime") ??
            sentAt;

        var bodyObject = GetObject(message, "Body");

        var bodyTextRaw =
            GetString(bodyObject, "Text") ??
            GetString(bodyObject, "Value") ??
            GetString(message, "Body") ??
            GetString(message, "BodyText") ??
            GetStringFromProperties(message, "Body") ??
            string.Empty;

        var bodyHtmlRaw =
            GetString(message, "BodyHtml") ??
            GetString(message, "HtmlBody") ??
            GetStringFromProperties(message, "HtmlBody");

        var bodyFormat =
            GetString(bodyObject, "Format") ??
            GetString(message, "BodyFormat");

        var cleanedBodyTextRaw = IsRtfBody(bodyTextRaw)
            ? ConvertRtfToPlainText(bodyTextRaw)
            : bodyTextRaw;

        var bodyHtml = !string.IsNullOrWhiteSpace(bodyHtmlRaw)
            ? bodyHtmlRaw
            : IsHtmlBody(bodyFormat, cleanedBodyTextRaw)
                ? cleanedBodyTextRaw
                : null;

        var bodyText = bodyHtml == null
            ? NormalizeWhitespace(cleanedBodyTextRaw)
            : NormalizeWhitespace(StripHtml(bodyHtml));

        var recipients = MapRecipients(recipientsObject);
        var attachments = MapAttachments(message);

        return new ParsedPstEmail(
            InternetMessageId: internetMessageId,
            FolderPath: string.IsNullOrWhiteSpace(folderPath) ? "Root" : folderPath,
            SenderEmail: senderEmail,
            SenderName: CleanNullable(senderName),
            Subject: CleanNullable(subject) ?? "(No subject)",
            BodyText: CleanNullable(bodyText),
            BodyHtml: CleanNullable(bodyHtml),
            SentAt: sentAt,
            ReceivedAt: receivedAt,
            Recipients: recipients,
            Attachments: attachments);
    }

    private static IReadOnlyCollection<ParsedPstRecipient> MapRecipients(object? recipientsObject)
    {
        var recipients = new List<ParsedPstRecipient>();

        AddRecipients(recipients, recipientsObject, "To", RecipientType.To);
        AddRecipients(recipients, recipientsObject, "Cc", RecipientType.Cc);
        AddRecipients(recipients, recipientsObject, "Bcc", RecipientType.Bcc);

        return recipients
            .GroupBy(x => new
            {
                x.RecipientType,
                RecipientEmail = x.RecipientEmail.ToLowerInvariant()
            })
            .Select(x => x.First())
            .ToList();
    }

    private static void AddRecipients(
        List<ParsedPstRecipient> recipients,
        object? recipientsObject,
        string propertyName,
        RecipientType recipientType)
    {
        foreach (var recipientObject in GetEnumerable(recipientsObject, propertyName))
        {
            var email = NormalizeEmail(GetRecipientEmail(recipientObject));

            if (string.IsNullOrWhiteSpace(email))
                continue;

            var name = CleanNullable(GetRecipientName(recipientObject));

            recipients.Add(new ParsedPstRecipient(
                recipientType,
                email,
                name));
        }
    }

    private static IReadOnlyCollection<ParsedPstAttachment> MapAttachments(object message)
    {
        var attachments = new List<ParsedPstAttachment>();

        foreach (var attachmentObject in GetEnumerable(message, "Attachments"))
        {
            try
            {
                var isFile = GetBool(attachmentObject, "IsFile");

                if (!isFile)
                    continue;

                var fileName =
                    GetString(attachmentObject, "FileName") ??
                    GetString(attachmentObject, "LongFileName") ??
                    GetString(attachmentObject, "DisplayName") ??
                    $"attachment-{Guid.NewGuid():N}.bin";

                var safeFileName = SanitizeFileName(fileName);

                var tempFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"xstreader-{Guid.NewGuid():N}-{safeFileName}");

                try
                {
                    SaveAttachmentToFile(attachmentObject, tempFilePath);

                    if (!File.Exists(tempFilePath))
                        continue;

                    var bytes = File.ReadAllBytes(tempFilePath);

                    if (bytes.Length == 0)
                        continue;

                    attachments.Add(new ParsedPstAttachment(
                        safeFileName,
                        GuessContentType(safeFileName),
                        bytes));
                }
                finally
                {
                    TryDeleteFile(tempFilePath);
                }
            }
            catch
            {
                // Skip broken attachment but keep parsing the message.
            }
        }

        return attachments;
    }

    private static void SaveAttachmentToFile(object attachmentObject, string filePath)
    {
        var attachmentType = attachmentObject.GetType();

        var saveMethods = attachmentType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.Name == "SaveToFile")
            .OrderBy(x => x.GetParameters().Length)
            .ToList();

        if (saveMethods.Count == 0)
            throw new InvalidOperationException("XstReader attachment does not expose SaveToFile method.");

        foreach (var method in saveMethods)
        {
            var parameters = method.GetParameters();

            try
            {
                if (parameters.Length == 1)
                {
                    method.Invoke(attachmentObject, new object?[] { filePath });
                    return;
                }

                if (parameters.Length == 2)
                {
                    var secondParameterValue = CreateDefaultValue(parameters[1].ParameterType);
                    method.Invoke(attachmentObject, new[] { filePath, secondParameterValue });
                    return;
                }
            }
            catch
            {
                // Try next overload.
            }
        }

        throw new InvalidOperationException("Unable to save XstReader attachment.");
    }

    private static object? CreateDefaultValue(Type type)
    {
        if (type == typeof(DateTime))
            return DateTime.UtcNow;

        if (type == typeof(DateTime?))
            return DateTime.UtcNow;

        if (type == typeof(bool))
            return false;

        if (type == typeof(string))
            return string.Empty;

        return type.IsValueType
            ? Activator.CreateInstance(type)
            : null;
    }

    private static string? GetRecipientEmail(object? recipientObject)
    {
        if (recipientObject == null)
            return null;

        return GetString(recipientObject, "EmailAddress") ??
               GetString(recipientObject, "Email") ??
               GetString(recipientObject, "SmtpAddress") ??
               GetString(recipientObject, "Address") ??
               GetString(recipientObject, "DisplayEmail") ??
               GetStringFromProperties(recipientObject, "EmailAddress") ??
               GetStringFromProperties(recipientObject, "SmtpAddress");
    }

    private static string? GetRecipientName(object? recipientObject)
    {
        if (recipientObject == null)
            return null;

        return GetString(recipientObject, "DisplayName") ??
               GetString(recipientObject, "Name") ??
               GetString(recipientObject, "RecipientName") ??
               GetStringFromProperties(recipientObject, "DisplayName");
    }

    private static IEnumerable<object> GetEnumerable(object? source, string propertyName)
    {
        if (source == null)
            yield break;

        var value = GetPropertyValue(source, propertyName);

        if (value is null)
            yield break;

        if (value is string)
            yield break;

        if (value is not IEnumerable enumerable)
            yield break;

        foreach (var item in enumerable)
        {
            if (item != null)
                yield return item;
        }
    }

    private static object? GetObject(object? source, string propertyName)
    {
        if (source == null)
            return null;

        return GetPropertyValue(source, propertyName);
    }

    private static string? GetString(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);

        return ValueToString(value);
    }

    private static DateTime? GetDateTime(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);

        return ValueToDateTime(value);
    }

    private static bool GetBool(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);

        return value switch
        {
            bool boolean => boolean,
            int number => number != 0,
            long number => number != 0,
            string text when bool.TryParse(text, out var parsed) => parsed,
            string text when int.TryParse(text, out var parsed) => parsed != 0,
            _ => false
        };
    }

    private static object? GetPropertyValue(object? source, string propertyName)
    {
        if (source == null)
            return null;

        var type = source.GetType();

        try
        {
            var property = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(source);

            var field = type.GetField(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (field != null)
                return field.GetValue(source);
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? GetStringFromProperties(object? source, string propertyName)
    {
        if (source == null)
            return null;

        var propertiesObject = GetPropertyValue(source, "Properties");

        if (propertiesObject == null)
            return null;

        foreach (var propertyObject in EnumerateObject(propertiesObject))
        {
            var name =
                GetString(propertyObject, "Name") ??
                GetString(propertyObject, "PropertyName") ??
                GetString(propertyObject, "Key") ??
                GetString(propertyObject, "TagName") ??
                GetString(propertyObject, "CanonicalName");

            if (!StringEqualsLoose(name, propertyName))
                continue;

            var value =
                GetPropertyValue(propertyObject, "Value") ??
                GetPropertyValue(propertyObject, "Data") ??
                GetPropertyValue(propertyObject, "PropertyValue");

            var text = ValueToString(value);

            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static DateTime? GetDateTimeFromProperties(object? source, string propertyName)
    {
        if (source == null)
            return null;

        var propertiesObject = GetPropertyValue(source, "Properties");

        if (propertiesObject == null)
            return null;

        foreach (var propertyObject in EnumerateObject(propertiesObject))
        {
            var name =
                GetString(propertyObject, "Name") ??
                GetString(propertyObject, "PropertyName") ??
                GetString(propertyObject, "Key") ??
                GetString(propertyObject, "TagName") ??
                GetString(propertyObject, "CanonicalName");

            if (!StringEqualsLoose(name, propertyName))
                continue;

            var value =
                GetPropertyValue(propertyObject, "Value") ??
                GetPropertyValue(propertyObject, "Data") ??
                GetPropertyValue(propertyObject, "PropertyValue");

            var date = ValueToDateTime(value);

            if (date.HasValue)
                return date;
        }

        return null;
    }

    private static IEnumerable<object> EnumerateObject(object? value)
    {
        if (value == null)
            yield break;

        if (value is string)
            yield break;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value != null)
                    yield return entry.Value;
            }

            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                    yield return item;
            }
        }
    }

    private static string? ValueToString(object? value)
    {
        return value switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => text.Trim(),
            DateTime dateTime => dateTime.ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
            byte[] bytes => bytes.Length == 0 ? null : Convert.ToBase64String(bytes),
            _ => value.ToString()?.Trim()
        };
    }

    private static DateTime? ValueToDateTime(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                : dateTime.ToUniversalTime(),
            DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime,
            string text when DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim();

        var match = Regex.Match(
            normalized,
            @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase);

        if (match.Success)
            return match.Value.ToLowerInvariant();

        if (normalized.Contains('@'))
            return normalized.ToLowerInvariant();

        return null;
    }

    private static string? CleanNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return NormalizeWhitespace(value);
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static bool IsHtmlBody(string? bodyFormat, string body)
    {
        if (!string.IsNullOrWhiteSpace(bodyFormat) &&
            bodyFormat.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return body.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("</", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutScripts = Regex.Replace(
            html,
            "<script.*?</script>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var withoutStyles = Regex.Replace(
            withoutScripts,
            "<style.*?</style>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var withoutTags = Regex.Replace(
            withoutStyles,
            "<.*?>",
            " ",
            RegexOptions.Singleline);

        return WebUtility.HtmlDecode(withoutTags);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var sanitized = new string(
            fileName
                .Select(character => invalidChars.Contains(character) ? '_' : character)
                .ToArray());

        sanitized = sanitized.Trim();

        return string.IsNullOrWhiteSpace(sanitized)
            ? $"attachment-{Guid.NewGuid():N}.bin"
            : sanitized;
    }

    private static string GuessContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".htm" => "text/html",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    private static bool StringEqualsLoose(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return false;

        static string Normalize(string value)
        {
            return Regex.Replace(value, @"[^a-zA-Z0-9]", string.Empty)
                .ToLowerInvariant();
        }

        return Normalize(left) == Normalize(right);
    }

    private static string GetInnermostMessage(Exception exception)
    {
        var current = exception;

        while (current.InnerException != null)
            current = current.InnerException;

        return current.Message;
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Ignore temp cleanup errors.
        }
    }
    
    private static bool IsRtfBody(string? body)
{
    if (string.IsNullOrWhiteSpace(body))
        return false;

    return body.TrimStart().StartsWith(@"{\rtf", StringComparison.OrdinalIgnoreCase);
}

private static string ConvertRtfToPlainText(string rtf)
{
    if (string.IsNullOrWhiteSpace(rtf))
        return string.Empty;

    var output = new StringBuilder();
    var ignoreStack = new Stack<bool>();

    var ignoreCurrentGroup = false;

    for (var i = 0; i < rtf.Length; i++)
    {
        var character = rtf[i];

        if (character == '{')
        {
            ignoreStack.Push(ignoreCurrentGroup);
            continue;
        }

        if (character == '}')
        {
            ignoreCurrentGroup = ignoreStack.Count > 0 && ignoreStack.Pop();
            continue;
        }

        if (character != '\\')
        {
            if (!ignoreCurrentGroup)
                output.Append(character);

            continue;
        }

        if (i + 1 >= rtf.Length)
            continue;

        var next = rtf[i + 1];

        if (next is '\\' or '{' or '}')
        {
            if (!ignoreCurrentGroup)
                output.Append(next);

            i++;
            continue;
        }

        if (next == '*')
        {
            ignoreCurrentGroup = true;
            i++;
            continue;
        }

        if (next == '~')
        {
            if (!ignoreCurrentGroup)
                output.Append(' ');

            i++;
            continue;
        }

        if (next == '-')
        {
            if (!ignoreCurrentGroup)
                output.Append('-');

            i++;
            continue;
        }

        if (next == '_')
        {
            if (!ignoreCurrentGroup)
                output.Append('-');

            i++;
            continue;
        }

        if (next == '\'')
        {
            if (i + 3 < rtf.Length)
            {
                var hex = rtf.Substring(i + 2, 2);

                if (byte.TryParse(
                        hex,
                        System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var value) &&
                    !ignoreCurrentGroup)
                {
                    output.Append((char)value);
                }

                i += 3;
            }

            continue;
        }

        if (!char.IsLetter(next))
        {
            i++;
            continue;
        }

        var controlStart = i + 1;
        var cursor = controlStart;

        while (cursor < rtf.Length && char.IsLetter(rtf[cursor]))
            cursor++;

        var controlWord = rtf[controlStart..cursor].ToLowerInvariant();

        var parameterStart = cursor;

        if (cursor < rtf.Length && (rtf[cursor] == '-' || rtf[cursor] == '+'))
            cursor++;

        while (cursor < rtf.Length && char.IsDigit(rtf[cursor]))
            cursor++;

        var parameter = rtf[parameterStart..cursor];

        if (cursor < rtf.Length && rtf[cursor] == ' ')
        {
            // RTF control-word delimiter space.
        }
        else
        {
            cursor--;
        }

        HandleRtfControlWord(
            output,
            controlWord,
            parameter,
            ref ignoreCurrentGroup);

        i = cursor;
    }

    var text = output.ToString();

    text = Regex.Replace(text, @"\r\n|\r|\n", "\n");
    text = Regex.Replace(text, @"[ \t]+", " ");
    text = Regex.Replace(text, @"\n\s+", "\n");
    text = Regex.Replace(text, @"(\n\s*){3,}", "\n\n");

    return NormalizeWhitespace(text);
}

private static void HandleRtfControlWord(
    StringBuilder output,
    string controlWord,
    string parameter,
    ref bool ignoreCurrentGroup)
{
    if (IsIgnoredRtfDestination(controlWord))
    {
        ignoreCurrentGroup = true;
        return;
    }

    if (ignoreCurrentGroup)
        return;

    switch (controlWord)
    {
        case "par":
        case "line":
            output.AppendLine();
            return;

        case "tab":
            output.Append('\t');
            return;

        case "emdash":
            output.Append('—');
            return;

        case "endash":
            output.Append('–');
            return;

        case "bullet":
            output.Append('•');
            return;

        case "lquote":
            output.Append('‘');
            return;

        case "rquote":
            output.Append('’');
            return;

        case "ldblquote":
            output.Append('“');
            return;

        case "rdblquote":
            output.Append('”');
            return;

        case "u":
            AppendUnicodeCharacter(output, parameter);
            return;
    }
}

private static bool IsIgnoredRtfDestination(string controlWord)
{
    return controlWord is
        "fonttbl" or
        "colortbl" or
        "stylesheet" or
        "info" or
        "pict" or
        "object" or
        "generator" or
        "xmlnstbl" or
        "datastore" or
        "themedata" or
        "colorschememapping";
}

private static void AppendUnicodeCharacter(StringBuilder output, string parameter)
{
    if (!int.TryParse(
            parameter,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value))
    {
        return;
    }

    if (value < 0)
        value += 65536;

    try
    {
        output.Append(char.ConvertFromUtf32(value));
    }
    catch
    {
        // Ignore invalid unicode escape.
    }
}
}