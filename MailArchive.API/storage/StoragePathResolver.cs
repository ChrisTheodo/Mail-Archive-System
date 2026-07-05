using MailArchive.Application.Abstractions;

namespace MailArchive.API.Storage;

public class StoragePathResolver : IStoragePathResolver
{
    private readonly IWebHostEnvironment _environment;

    public StoragePathResolver(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string ResolvePath(string storagePath)
    {
        if (Path.IsPathRooted(storagePath))
            return storagePath;

        return Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            storagePath));
    }
}