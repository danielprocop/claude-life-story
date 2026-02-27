namespace DiarioIntelligente.Core.DTOs;

public class AiAnalysisResult
{
    public List<string> Emotions { get; set; } = new();
    public List<ExtractedConcept> Concepts { get; set; } = new();
    public List<GoalSignal> GoalSignals { get; set; } = new();
    public List<GoalCompletion> GoalCompletions { get; set; } = new();
    public int EnergyLevel { get; set; } = 5; // 1-10
    public int StressLevel { get; set; } = 5; // 1-10
}

public class ExtractedConcept
{
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class GoalSignal
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class GoalCompletion
{
    public string Text { get; set; } = string.Empty;
    public string MatchesDesire { get; set; } = string.Empty;
}
