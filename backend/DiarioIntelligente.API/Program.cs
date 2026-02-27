using DiarioIntelligente.AI;
using DiarioIntelligente.API.Services;
using DiarioIntelligente.Infrastructure;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure (EF Core + repositories)
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));

// AI services (OpenAI)
builder.Services.AddAiServices(builder.Configuration);

// Background processing queue
builder.Services.AddSingleton<EntryProcessingQueue>();
builder.Services.AddHostedService<EntryProcessingService>();

// CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto-create database in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Seed demo user if not exists
    if (!await db.Users.AnyAsync(u => u.Id == Guid.Parse("00000000-0000-0000-0000-000000000001")))
    {
        db.Users.Add(new DiarioIntelligente.Core.Models.User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "demo@diariointelligente.app",
            PasswordHash = "demo", // Not a real hash — demo only
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.MapControllers();

app.Run();
