namespace DiarioIntelligente.Infrastructure.Options;

public sealed class SearchBackendOptions
{
    public bool Enabled { get; set; }
    public string Region { get; set; } = "eu-west-1";
    public string Endpoint { get; set; } = string.Empty;
    public string EntityIndex { get; set; } = "diario-entities";
    public string EntryIndex { get; set; } = "diario-entries";
    public string GoalIndex { get; set; } = "diario-goals";
}
