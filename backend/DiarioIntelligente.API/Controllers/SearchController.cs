using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : AuthenticatedController
{
    private readonly IEntryRepository _entryRepository;
    private readonly IConceptRepository _conceptRepository;
    private readonly IGoalItemRepository _goalItemRepository;

    public SearchController(
        IEntryRepository entryRepository,
        IConceptRepository conceptRepository,
        IGoalItemRepository goalItemRepository)
    {
        _entryRepository = entryRepository;
        _conceptRepository = conceptRepository;
        _goalItemRepository = goalItemRepository;
    }

    [HttpGet]
    public async Task<ActionResult<SearchResponse>> Search([FromQuery] string q, [FromQuery] int limit = 8)
    {
        var query = q?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(new SearchResponse(query, new List<EntrySearchHit>(), new List<ConceptSearchHit>(), new List<GoalItemSearchHit>()));
        }

        var userId = GetUserId();
        var safeLimit = Math.Clamp(limit, 1, 20);

        var entries = await _entryRepository.SearchAsync(userId, query, safeLimit);
        var concepts = await _conceptRepository.SearchAsync(userId, query, safeLimit);
        var goalItems = await _goalItemRepository.SearchAsync(userId, query, safeLimit);

        return Ok(new SearchResponse(
            query,
            entries.Select(e => new EntrySearchHit(
                e.Id,
                e.Content.Length > 180 ? e.Content[..180] + "..." : e.Content,
                e.CreatedAt,
                e.HasPendingDerivedData ? 0 : e.EntryConceptMaps.Count
            )).ToList(),
            concepts.Select(c => new ConceptSearchHit(
                c.Id,
                c.Label,
                c.Type,
                c.EntryConceptMaps.Count,
                c.LastSeenAt
            )).ToList(),
            goalItems.Select(g => new GoalItemSearchHit(
                g.Id,
                g.Title,
                g.Description,
                g.Status,
                g.CreatedAt,
                g.SubGoals.Count
            )).ToList()
        ));
    }
}
