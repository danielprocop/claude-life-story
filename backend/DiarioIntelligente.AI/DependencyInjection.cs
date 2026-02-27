using DiarioIntelligente.AI.Configuration;
using DiarioIntelligente.AI.Services;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiarioIntelligente.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiSettings>(configuration.GetSection(OpenAiSettings.SectionName));

        var apiKey = configuration.GetSection("OpenAI:ApiKey").Value;
        if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_API_KEY_HERE")
        {
            services.AddSingleton<IAiService, OpenAiService>();
        }
        else
        {
            services.AddSingleton<IAiService, NoOpAiService>();
        }

        return services;
    }
}
