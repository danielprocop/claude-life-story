using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NodesController : AuthenticatedController
{
    private readonly ICognitiveGraphService _cognitiveGraphService;

    public NodesController(ICognitiveGraphService cognitiveGraphService)
    {
        _cognitiveGraphService = cognitiveGraphService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NodeViewResponse>> Get(Guid id)
    {
        var node = await _cognitiveGraphService.GetNodeViewAsync(GetUserId(), id, HttpContext.RequestAborted);
        if (node == null) return NotFound();
        return Ok(node);
    }
}
