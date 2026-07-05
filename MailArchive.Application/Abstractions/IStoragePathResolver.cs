namespace MailArchive.Application.Abstractions;

public interface IStoragePathResolver
{
    string ResolvePath(string storagePath);
}