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
    private readonly EntryProcessingQueue _processingQueue;
    private readonly UserMemoryRebuildQueue _rebuildQueue;
    private readonly ISearchProjectionService _searchProjectionService;

    public EntriesController(
        IEntryRepository entryRepo,
        EntryProcessingQueue processingQueue,
        UserMemoryRebuildQueue rebuildQueue,
        ISearchProjectionService searchProjectionService)
    {
        _entryRepo = entryRepo;
        _processingQueue = processingQueue;
        _rebuildQueue = rebuildQueue;
        _searchProjectionService = searchProjectionService;
    }

    [HttpPost]
    public async Task<ActionResult<EntryResponse>> Create([FromBody] CreateEntryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Il contenuto e obbligatorio." });

        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            UserId = GetUserId(),
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _entryRepo.CreateAsync(entry);

        // Enqueue for background AI processing
        await _processingQueue.EnqueueAsync(new EntryProcessingJob(entry.Id, entry.UserId));

        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, new EntryResponse(
            entry.Id,
            entry.Content,
            entry.CreatedAt,
            entry.UpdatedAt,
            null
        ));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EntryResponse>> Update(Guid id, [FromBody] UpdateEntryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Il contenuto e obbligatorio." });

        var entry = await _entryRepo.GetByIdAsync(id, GetUserId());
        if (entry == null) return NotFound();

        entry.Content = request.Content.Trim();
        entry.UpdatedAt = DateTime.UtcNow;
        entry.EmbeddingVector = null;

        await _entryRepo.UpdateAsync(entry);
        await _rebuildQueue.EnqueueAsync(entry.UserId, HttpContext.RequestAborted);

        return Ok(new EntryResponse(
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

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var entry = await _entryRepo.GetByIdAsync(id, GetUserId());
        if (entry == null) return NotFound();

        var deleted = await _entryRepo.DeleteAsync(id, entry.UserId);
        if (!deleted) return NotFound();

        await _searchProjectionService.DeleteEntryAsync(id, entry.UserId, HttpContext.RequestAborted);
        await _rebuildQueue.EnqueueAsync(entry.UserId, HttpContext.RequestAborted);
        return NoContent();
    }
}
