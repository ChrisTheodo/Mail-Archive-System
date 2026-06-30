using FluentValidation;
using MailArchive.Application;
using MailArchive.Application.Abstractions;
using MailArchive.Persistence;
using MailArchive.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using MailArchive.Application.Users;
using FluentValidation.AspNetCore;
using MailArchive.Application.Mailboxes;
using MailArchive.Application.Users.Validators;
using MailArchive.API.Middleware;

var builder = WebApplication.CreateBuilder(args);



// Controllers
builder.Services.AddControllers();

builder.Services.AddDbContext<MailArchiveDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<MailArchiveSettings>(
    builder.Configuration.GetSection("MailArchive"));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMailboxService, MailboxService>();
builder.Services.AddScoped<IMailArchiveDbContext>(sp =>
    sp.GetRequiredService<MailArchiveDbContext>());


builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Logging.ClearProviders();
builder.Logging.AddConsole();


var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();


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