using MailArchive.Application;
using MailArchive.Persistence;
using MailArchive.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

builder.Services.AddDbContext<MailArchiveDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<MailArchiveSettings>(
    builder.Configuration.GetSection("MailArchive"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();


// DEV: DB Migration + Seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MailArchiveDbContext>();

    await db.Database.MigrateAsync();   // <-- ΕΔΩ (ΟΧΙ στο seeder)
    await DataSeeder.SeedAsync(db);
}


// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => "OK");

app.MapGet("/health/db", async (MailArchiveDbContext db) =>
{
    return await db.Users.CountAsync();
});

app.Run();