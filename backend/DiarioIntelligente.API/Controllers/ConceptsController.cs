using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[Route("api/[controller]")]
public class ConceptsController : AuthenticatedController
{
    private readonly IConceptRepository _conceptRepo;

    public ConceptsController(IConceptRepository conceptRepo) => _conceptRepo = conceptRepo;

    [HttpGet]
    public async Task<ActionResult<List<ConceptResponse>>> GetAll()
    {
        var concepts = await _conceptRepo.GetByUserAsync(GetUserId());

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
