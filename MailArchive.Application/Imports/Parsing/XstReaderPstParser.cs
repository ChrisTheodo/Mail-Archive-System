using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using MailArchive.Domain.Enums;
using XstReader;

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

        var parsedEmails = new List<ParsedPstEmail>();

        using var xstFile = new XstFile(pstFilePath);

        TraverseFolder(
            folder: xstFile.RootFolder,
            parentPath: string.Empty,
            parsedEmails: parsedEmails);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyCollection<ParsedPstEmail>>(parsedEmails);
    }

    private static void TraverseFolder(
        object folder,
        string parentPath,
        List<ParsedPstEmail> parsedEmails)
    {
        var folderName =
            GetString(folder, "Name") ??
            GetString(folder, "DisplayName") ??
            "Folder";

        var folderPath = string.IsNullOrWhiteSpace(parentPath)
            ? folderName
            : $"{parentPath}/{folderName}";

        foreach (var message in GetEnumerable(folder, "Messages"))
        {
            var parsedEmail = MapMessage(message, folderPath);
            parsedEmails.Add(parsedEmail);
        }

        foreach (var childFolder in GetEnumerable(folder, "Folders"))
        {
            TraverseFolder(childFolder, folderPath, parsedEmails);
        }

        TryCall(folder, "ClearContents");
    }

    private static ParsedPstEmail MapMessage(object message, string folderPath)
    {
        var recipientsObject = GetObject(message, "Recipients");
        var senderObject =
            GetObject(recipientsObject, "Sender") ??
            GetObject(recipientsObject, "Originator") ??
            GetObject(recipientsObject, "SentRepresenting");

        var senderEmail =
            GetRecipientEmail(senderObject) ??
            GetString(message, "SenderEmail") ??
            GetString(message, "SenderEmailAddress") ??
            GetString(message, "FromEmail") ??
            "unknown@unknown.local";

        var senderName =
            GetRecipientName(senderObject) ??
            GetString(message, "SenderName") ??
            GetString(message, "FromName");

        var subject =
            GetString(message, "Subject") ??
            GetStringFromProperties(message, "Subject") ??
            "(No subject)";

        var internetMessageId =
            GetString(message, "InternetMessageId") ??
            GetStringFromProperties(message, "InternetMessageId") ??
            $"<{Guid.NewGuid():N}@xstreader.local>";

        var sentAt =
            GetDateTime(message, "SentAt") ??
            GetDateTime(message, "SubmitTime") ??
            GetDateTime(message, "ClientSubmitTime") ??
            GetDateTime(message, "DeliveryTime");

        var receivedAt =
            GetDateTime(message, "ReceivedAt") ??
            GetDateTime(message, "DeliveryTime") ??
            sentAt;

        var bodyObject = GetObject(message, "Body");
        var bodyTextRaw =
            GetString(bodyObject, "Text") ??
            GetString(message, "Body") ??
            string.Empty;

        var bodyFormat = GetString(bodyObject, "Format");

        var bodyHtml = IsHtmlBody(bodyFormat, bodyTextRaw)
            ? bodyTextRaw
            : null;

        var bodyText = bodyHtml == null
            ? bodyTextRaw
            : StripHtml(bodyHtml);

        var recipients = MapRecipients(recipientsObject);
        var attachments = MapAttachments(message);

        return new ParsedPstEmail(
            internetMessageId,
            folderPath,
            senderEmail.Trim().ToLowerInvariant(),
            senderName,
            subject,
            bodyText,
            bodyHtml,
            sentAt,
            receivedAt,
            recipients,
            attachments);
    }

    private static IReadOnlyCollection<ParsedPstRecipient> MapRecipients(object? recipientsObject)
    {
        var recipients = new List<ParsedPstRecipient>();

        AddRecipients(
            recipients,
            recipientsObject,
            "To",
            RecipientType.To);

        AddRecipients(
            recipients,
            recipientsObject,
            "Cc",
            RecipientType.Cc);

        AddRecipients(
            recipients,
            recipientsObject,
            "Bcc",
            RecipientType.Bcc);

        return recipients;
    }

    private static void AddRecipients(
        List<ParsedPstRecipient> recipients,
        object? recipientsObject,
        string propertyName,
        RecipientType recipientType)
    {
        foreach (var recipientObject in GetEnumerable(recipientsObject, propertyName))
        {
            var email = GetRecipientEmail(recipientObject);

            if (string.IsNullOrWhiteSpace(email))
                continue;

            var name = GetRecipientName(recipientObject);

            recipients.Add(new ParsedPstRecipient(
                recipientType,
                email.Trim().ToLowerInvariant(),
                name));
        }
    }

    private static IReadOnlyCollection<ParsedPstAttachment> MapAttachments(object message)
    {
        var attachments = new List<ParsedPstAttachment>();

        foreach (var attachmentObject in GetEnumerable(message, "Attachments"))
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
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
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
               GetStringFromProperties(recipientObject, "EmailAddress");
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

        return value switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => text.Trim(),
            _ => value.ToString()
        };
    }

    private static DateTime? GetDateTime(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);

        if (value == null)
            return null;

        if (value is DateTime dateTime)
            return dateTime;

        if (DateTime.TryParse(value.ToString(), out var parsed))
            return parsed;

        return null;
    }

    private static bool GetBool(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);

        if (value is bool boolValue)
            return boolValue;

        if (bool.TryParse(value?.ToString(), out var parsed))
            return parsed;

        return false;
    }

    private static object? GetPropertyValue(object? source, string propertyName)
    {
        if (source == null)
            return null;

        var property = source
            .GetType()
            .GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase);

        return property?.GetValue(source);
    }

    private static string? GetStringFromProperties(object source, string propertyName)
    {
        var properties = GetPropertyValue(source, "Properties");

        if (properties == null)
            return null;

        var directValue = GetPropertyValue(properties, propertyName);

        if (directValue != null)
            return directValue.ToString();

        foreach (var propertyObject in GetEnumerable(properties, "Values"))
        {
            var name =
                GetString(propertyObject, "Name") ??
                GetString(propertyObject, "TagName") ??
                GetString(propertyObject, "PropertyName");

            if (!string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            var value =
                GetPropertyValue(propertyObject, "Value") ??
                GetPropertyValue(propertyObject, "Data");

            return value?.ToString();
        }

        return null;
    }

    private static void TryCall(object source, string methodName)
    {
        var method = source
            .GetType()
            .GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.IgnoreCase,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

        method?.Invoke(source, null);
    }

    private static bool IsHtmlBody(string? bodyFormat, string body)
    {
        if (!string.IsNullOrWhiteSpace(bodyFormat) &&
            bodyFormat.Contains("Html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return body.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("<body", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutTags = Regex.Replace(html, "<.*?>", " ");
        var normalized = Regex.Replace(withoutTags, @"\s+", " ");

        return normalized.Trim();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var sanitized = new string(fileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? $"attachment-{Guid.NewGuid():N}.bin"
            : sanitized;
    }

    private static string? GuessContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip" => "application/zip",
            _ => null
        };
    }
}