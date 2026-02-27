using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConceptsController : ControllerBase
{
    private readonly IConceptRepository _conceptRepo;

    private static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ConceptsController(IConceptRepository conceptRepo) => _conceptRepo = conceptRepo;

    [HttpGet]
    public async Task<ActionResult<List<ConceptResponse>>> GetAll()
    {
        var concepts = await _conceptRepo.GetByUserAsync(DemoUserId);

        return Ok(concepts.Select(c => new ConceptResponse(
            c.Id,
            c.Label,
            c.Type,
            c.FirstSeenAt,
            c.LastSeenAt,
            c.EntryConceptMaps.Count
        )).ToList());
    }
}
