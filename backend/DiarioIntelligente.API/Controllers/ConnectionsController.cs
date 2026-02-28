using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectionsController : AuthenticatedController
{
    private readonly IConnectionRepository _connectionRepo;
    private readonly IConceptRepository _conceptRepo;

    public ConnectionsController(IConnectionRepository connectionRepo, IConceptRepository conceptRepo)
    {
        _connectionRepo = connectionRepo;
        _conceptRepo = conceptRepo;
    }

    [HttpGet]
    public async Task<ActionResult<GraphResponse>> GetGraph()
    {
        var userId = GetUserId();
        var concepts = await _conceptRepo.GetByUserAsync(userId);
        var connections = await _connectionRepo.GetByUserConceptsAsync(userId);

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
