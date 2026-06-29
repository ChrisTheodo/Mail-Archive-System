namespace MailArchive.Persistence;
using Microsoft.EntityFrameworkCore;

public class MailArchiveDbContext : DbContext
{
    public MailArchiveDbContext(DbContextOptions<MailArchiveDbContext> options)
        : base(options)
    {
    }
}