using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiarioIntelligente.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IEntryRepository, EntryRepository>();
        services.AddScoped<IConceptRepository, ConceptRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<IInsightRepository, InsightRepository>();
        services.AddScoped<IEnergyLogRepository, EnergyLogRepository>();
        services.AddScoped<IGoalItemRepository, GoalItemRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();

        return services;
    }
}
