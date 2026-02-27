namespace DiarioIntelligente.Core.DTOs;

public record GraphResponse(
    List<GraphNode> Nodes,
    List<GraphEdge> Edges
);

public record GraphNode(
    Guid Id,
    string Label,
    string Type,
    int Weight
);

public record GraphEdge(
    Guid SourceId,
    Guid TargetId,
    float Strength,
    string Type
);
