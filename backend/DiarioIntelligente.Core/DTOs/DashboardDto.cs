namespace DiarioIntelligente.Core.DTOs;

public record DashboardResponse(
    DashboardStats Stats,
    List<EnergyDataPoint> EnergyTrend,
    List<ConceptResponse> TopConcepts,
    List<GoalItemResponse> ActiveGoals,
    List<InsightResponse> RecentInsights,
    List<EntryListResponse> RecentEntries
);

public record DashboardStats(
    int TotalEntries,
    int TotalConcepts,
    int ActiveGoals,
    int InsightsGenerated,
    double AvgEnergy,
    double AvgStress
);
