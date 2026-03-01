using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using DiarioIntelligente.Infrastructure.Options;
using DiarioIntelligente.Infrastructure.Repositories;
using DiarioIntelligente.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;

namespace DiarioIntelligente.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string databaseProvider = "Sqlite",
        IConfiguration? configuration = null)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                options.UseNpgsql(connectionString);
            else
                options.UseSqlite(connectionString);
        });

        var searchOptions = BuildSearchOptions(configuration);
        services.AddSingleton(searchOptions);

        services.AddScoped<IEntryRepository, EntryRepository>();
        services.AddScoped<IConceptRepository, ConceptRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<IInsightRepository, InsightRepository>();
        services.AddScoped<IEnergyLogRepository, EnergyLogRepository>();
        services.AddScoped<IGoalItemRepository, GoalItemRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<ICognitiveGraphService, CognitiveGraphService>();
        services.AddScoped<IEntityNormalizationService, EntityNormalizationService>();
        services.AddScoped<IPersonalModelService, PersonalModelService>();
        services.AddScoped<ILedgerQueryService, LedgerQueryService>();
        services.AddScoped<IClarificationService, ClarificationService>();
        services.AddScoped<IFeedbackPolicyService, FeedbackPolicyService>();
        services.AddScoped<IFeedbackAdminService, FeedbackAdminService>();

        if (searchOptions.Enabled)
        {
            services.AddSingleton<IOpenSearchClient>(_ => OpenSearchClientFactory.Create(searchOptions));
            services.AddScoped<ISearchProjectionService, OpenSearchProjectionService>();
            services.AddScoped<IEntityRetrievalService, OpenSearchEntityRetrievalService>();
        }
        else
        {
            services.AddScoped<ISearchProjectionService, NoOpSearchProjectionService>();
            services.AddScoped<IEntityRetrievalService, NoOpEntityRetrievalService>();
        }

        return services;
    }

    private static SearchBackendOptions BuildSearchOptions(IConfiguration? configuration)
    {
        var options = new SearchBackendOptions();
        if (configuration != null)
        {
            var enabledRaw = configuration["Search:Enabled"];
            if (!string.IsNullOrWhiteSpace(enabledRaw) && bool.TryParse(enabledRaw, out var parsedEnabled))
                options.Enabled = parsedEnabled;

            options.Region = configuration["Search:Region"] ?? options.Region;
            options.Endpoint = configuration["Search:Endpoint"] ?? options.Endpoint;
            options.EntityIndex = configuration["Search:EntityIndex"] ?? options.EntityIndex;
            options.EntryIndex = configuration["Search:EntryIndex"] ?? options.EntryIndex;
            options.GoalIndex = configuration["Search:GoalIndex"] ?? options.GoalIndex;
        }

        var enabledFromEnv = Environment.GetEnvironmentVariable("Search__Enabled");
        if (!string.IsNullOrWhiteSpace(enabledFromEnv) && bool.TryParse(enabledFromEnv, out var enabled))
            options.Enabled = enabled;

        var endpointFromEnv = Environment.GetEnvironmentVariable("Search__Endpoint");
        if (!string.IsNullOrWhiteSpace(endpointFromEnv))
            options.Endpoint = endpointFromEnv.Trim();

        var regionFromEnv = Environment.GetEnvironmentVariable("Search__Region");
        if (!string.IsNullOrWhiteSpace(regionFromEnv))
            options.Region = regionFromEnv.Trim();

        var entityIndexFromEnv = Environment.GetEnvironmentVariable("Search__EntityIndex");
        if (!string.IsNullOrWhiteSpace(entityIndexFromEnv))
            options.EntityIndex = entityIndexFromEnv.Trim();

        var entryIndexFromEnv = Environment.GetEnvironmentVariable("Search__EntryIndex");
        if (!string.IsNullOrWhiteSpace(entryIndexFromEnv))
            options.EntryIndex = entryIndexFromEnv.Trim();

        var goalIndexFromEnv = Environment.GetEnvironmentVariable("Search__GoalIndex");
        if (!string.IsNullOrWhiteSpace(goalIndexFromEnv))
            options.GoalIndex = goalIndexFromEnv.Trim();

        if (!options.Enabled && !string.IsNullOrWhiteSpace(options.Endpoint))
            options.Enabled = true;

        return options;
    }
}
