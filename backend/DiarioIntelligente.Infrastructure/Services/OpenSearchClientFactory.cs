using Amazon;
using Amazon.Runtime;
using DiarioIntelligente.Infrastructure.Options;
using OpenSearch.Client;
using OpenSearch.Net.Auth.AwsSigV4;

namespace DiarioIntelligente.Infrastructure.Services;

public static class OpenSearchClientFactory
{
    public static IOpenSearchClient Create(SearchBackendOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new InvalidOperationException("Search endpoint is required when OpenSearch is enabled.");

        var endpoint = options.Endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? options.Endpoint
            : $"https://{options.Endpoint}";

        var region = RegionEndpoint.GetBySystemName(options.Region);
        var credentials = FallbackCredentialsFactory.GetCredentials();
        var connection = new AwsSigV4HttpConnection(credentials, region);

        var settings = new ConnectionSettings(new Uri(endpoint), connection)
            .DisableDirectStreaming();

        return new OpenSearchClient(settings);
    }
}
