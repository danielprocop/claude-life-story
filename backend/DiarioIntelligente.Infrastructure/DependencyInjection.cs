using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Repositories;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiarioIntelligente.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string databaseProvider = "Sqlite")
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                options.UseNpgsql(connectionString);
            else
                options.UseSqlite(connectionString);
        });

        services.AddScoped<IEntryRepository, EntryRepository>();
        services.AddScoped<IConceptRepository, ConceptRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<IInsightRepository, InsightRepository>();
        services.AddScoped<IEnergyLogRepository, EnergyLogRepository>();
        services.AddScoped<IGoalItemRepository, GoalItemRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<ISearchProjectionService, NoOpSearchProjectionService>();
        services.AddScoped<ICognitiveGraphService, CognitiveGraphService>();
        services.AddScoped<IPersonalModelService, PersonalModelService>();

        return services;
    }
}
