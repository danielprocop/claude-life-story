using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectionsController : ControllerBase
{
    private readonly IConnectionRepository _connectionRepo;
    private readonly IConceptRepository _conceptRepo;

    private static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ConnectionsController(IConnectionRepository connectionRepo, IConceptRepository conceptRepo)
    {
        _connectionRepo = connectionRepo;
        _conceptRepo = conceptRepo;
    }

    [HttpGet]
    public async Task<ActionResult<GraphResponse>> GetGraph()
    {
        var concepts = await _conceptRepo.GetByUserAsync(DemoUserId);
        var connections = await _connectionRepo.GetByUserConceptsAsync(DemoUserId);

        var nodes = concepts.Select(c => new GraphNode(
            c.Id,
            c.Label,
            c.Type,
            c.EntryConceptMaps.Count
        )).ToList();

        var edges = connections.Select(c => new GraphEdge(
            c.ConceptAId,
            c.ConceptBId,
            c.Strength,
            c.Type
        )).ToList();

        return Ok(new GraphResponse(nodes, edges));
    }
}
