using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Persistence.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(MailArchiveDbContext context)
    {
        // Αν υπάρχουν users, δεν ξανακάνουμε seed
        if (await context.Users.AnyAsync())
            return;

        var adminUser = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email = "admin@mailarchive.local",
            DisplayName = "System Admin",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var normalUser = new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Email = "user@mailarchive.local",
            DisplayName = "Test User",
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await context.Users.AddRangeAsync(adminUser, normalUser);
        await context.SaveChangesAsync();
    }
}