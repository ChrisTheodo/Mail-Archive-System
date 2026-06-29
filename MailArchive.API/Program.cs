using MailArchive.Application;
using MailArchive.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();


builder.Services.AddDbContext<MailArchiveDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<MailArchiveSettings>(builder.Configuration.GetSection("MailArchive"));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

Console.WriteLine("API STARTING...");

var app = builder.Build();


// Dev tools
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Routing
app.UseRouting();

// Authorization (θα ενεργοποιηθεί αργότερα)
app.UseAuthorization();

// Map controllers
app.MapControllers();


app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine(">>> APP STARTED EVENT FIRED");
});

Console.WriteLine("AFTER BUILD");
app.Run();