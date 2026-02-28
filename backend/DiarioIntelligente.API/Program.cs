using DiarioIntelligente.AI;
using DiarioIntelligente.API.Services;
using DiarioIntelligente.Infrastructure;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database provider from config (default: Sqlite for local dev)
var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

// Connection string: env var takes precedence
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Infrastructure (EF Core + repositories)
builder.Services.AddInfrastructure(connectionString, databaseProvider);

// AI services: env var overrides appsettings for API key
var openAiKeyFromEnv = Environment.GetEnvironmentVariable("OpenAI__ApiKey");
if (!string.IsNullOrEmpty(openAiKeyFromEnv))
    builder.Configuration["OpenAI:ApiKey"] = openAiKeyFromEnv;
builder.Services.AddAiServices(builder.Configuration);

// Background processing queue
builder.Services.AddSingleton<EntryProcessingQueue>();
builder.Services.AddHostedService<EntryProcessingService>();
builder.Services.AddScoped<CurrentUserService>();

// Cognito JWT Authentication
var cognitoUserPoolId = Environment.GetEnvironmentVariable("Cognito__UserPoolId")
    ?? builder.Configuration.GetValue<string>("Cognito:UserPoolId") ?? "";
var cognitoClientId = Environment.GetEnvironmentVariable("Cognito__ClientId")
    ?? builder.Configuration.GetValue<string>("Cognito:ClientId") ?? "";
var cognitoRegion = Environment.GetEnvironmentVariable("Cognito__Region")
    ?? builder.Configuration.GetValue<string>("Cognito:Region") ?? "eu-west-1";

if (!string.IsNullOrEmpty(cognitoUserPoolId))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = cognitoClientId,
                ValidateLifetime = true
            };
        });
    builder.Services.AddAuthorization();
}

// CORS: configurable origins (default: localhost:4200 for local dev)
var allowedOrigins = builder.Configuration.GetValue<string>("Cors:AllowedOrigins")
    ?? "http://localhost:4200";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto-create database and seed demo user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (!await db.Users.AnyAsync(u => u.Id == Guid.Parse("00000000-0000-0000-0000-000000000001")))
    {
        db.Users.Add(new DiarioIntelligente.Core.Models.User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "demo@diariointelligente.app",
            PasswordHash = "demo",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

// Swagger only in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

if (!string.IsNullOrEmpty(cognitoUserPoolId))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next();
        return;
    }

    var currentUserService = context.RequestServices.GetRequiredService<CurrentUserService>();
    var currentUser = await currentUserService.EnsureUserAsync(context.User, context.RequestAborted);
    context.Items[CurrentUserContext.HttpContextItemKey] = currentUser;

    await next();
});

// Health check for App Runner
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapControllers();

app.Run();
