namespace DiarioIntelligente.Core.DTOs;

public record EnergyLogResponse(
    Guid Id,
    int EnergyLevel,
    int StressLevel,
    string? DominantEmotion,
    DateTime RecordedAt
);

public record EnergyTrendResponse(
    List<EnergyDataPoint> DataPoints,
    double AvgEnergy,
    double AvgStress,
    List<string> TopEmotions,
    List<EnergyCorrelation> Correlations
);

public record EnergyDataPoint(
    DateTime Date,
    int Energy,
    int Stress,
    string? Emotion
);

public record EnergyCorrelation(
    string Factor,
    string Effect,
    float Confidence
);
