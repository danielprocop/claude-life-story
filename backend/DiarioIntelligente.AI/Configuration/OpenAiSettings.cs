namespace DiarioIntelligente.AI.Configuration;

public class OpenAiSettings
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string InsightModel { get; set; } = "gpt-4o";
}
