using DiarioIntelligente.API.Services;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[Route("api/[controller]")]
public class EntriesController : AuthenticatedController
{
    private readonly IEntryRepository _entryRepo;
    private readonly EntryProcessingQueue _queue;

    public EntriesController(IEntryRepository entryRepo, EntryProcessingQueue queue)
    {
        _entryRepo = entryRepo;
        _queue = queue;
    }

    [HttpPost]
    public async Task<ActionResult<EntryResponse>> Create([FromBody] CreateEntryRequest request)
    {
        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            UserId = GetUserId(),
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };

        await _entryRepo.CreateAsync(entry);

        // Enqueue for background AI processing
        await _queue.EnqueueAsync(new EntryProcessingJob(entry.Id, entry.UserId, entry.Content));

        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, new EntryResponse(
            entry.Id,
            entry.Content,
            entry.CreatedAt,
            entry.UpdatedAt,
            null
        ));
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<EntryListResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var (items, totalCount) = await _entryRepo.GetByUserAsync(GetUserId(), page, pageSize);

        var response = new PaginatedResponse<EntryListResponse>(
            items.Select(e => new EntryListResponse(
                e.Id,
                e.Content.Length > 150 ? e.Content[..150] + "..." : e.Content,
                e.CreatedAt,
                e.EntryConceptMaps.Count
            )).ToList(),
            totalCount,
            page,
            pageSize
        );

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EntryResponse>> GetById(Guid id)
    {
        var entry = await _entryRepo.GetByIdAsync(id, GetUserId());
        if (entry == null) return NotFound();

        return Ok(new EntryResponse(
            entry.Id,
            entry.Content,
            entry.CreatedAt,
            entry.UpdatedAt,
            entry.EntryConceptMaps.Select(m => new ConceptResponse(
                m.Concept.Id,
                m.Concept.Label,
                m.Concept.Type,
                m.Concept.FirstSeenAt,
                m.Concept.LastSeenAt,
                m.Concept.EntryConceptMaps?.Count ?? 0
            )).ToList()
        ));
    }

    [HttpGet("{id:guid}/related")]
    public async Task<ActionResult<List<RelatedEntryResponse>>> GetRelated(Guid id, [FromQuery] int limit = 6)
    {
        var related = await _entryRepo.GetRelatedAsync(id, GetUserId(), Math.Clamp(limit, 1, 12));

        return Ok(related.Select(x => new RelatedEntryResponse(
            x.Entry.Id,
            x.Entry.Content.Length > 140 ? x.Entry.Content[..140] + "..." : x.Entry.Content,
            x.Entry.CreatedAt,
            x.SharedConceptCount
        )).ToList());
    }
}
