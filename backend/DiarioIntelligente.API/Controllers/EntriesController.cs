using DiarioIntelligente.API.Services;
using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace DiarioIntelligente.API.Controllers;

[Route("api/[controller]")]
public class EntriesController : AuthenticatedController
{
    private readonly IEntryRepository _entryRepo;
    private readonly EntryProcessingQueue _processingQueue;
    private readonly UserMemoryRebuildQueue _rebuildQueue;
    private readonly ISearchProjectionService _searchProjectionService;
    private readonly AppDbContext _db;

    public EntriesController(
        IEntryRepository entryRepo,
        EntryProcessingQueue processingQueue,
        UserMemoryRebuildQueue rebuildQueue,
        ISearchProjectionService searchProjectionService,
        AppDbContext db)
    {
        _entryRepo = entryRepo;
        _processingQueue = processingQueue;
        _rebuildQueue = rebuildQueue;
        _searchProjectionService = searchProjectionService;
        _db = db;
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
                e.HasPendingDerivedData ? 0 : e.EntryConceptMaps.Count
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
            entry.HasPendingDerivedData
                ? null
                : entry.EntryConceptMaps.Select(m => new ConceptResponse(
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
        var entry = await _entryRepo.GetByIdAsync(id, GetUserId());
        if (entry == null) return NotFound();
        if (entry.HasPendingDerivedData) return Ok(new List<RelatedEntryResponse>());

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

    [HttpPost("{id:guid}/feedback/entity")]
    public async Task<ActionResult<EntryEntityFeedbackResponse>> SubmitEntityFeedback(
        Guid id,
        [FromBody] EntryEntityFeedbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return BadRequest(new { error = "Il campo label e obbligatorio." });

        if (string.IsNullOrWhiteSpace(request.ExpectedKind))
            return BadRequest(new { error = "Il campo expectedKind e obbligatorio." });

        var entry = await _entryRepo.GetByIdAsync(id, GetUserId());
        if (entry == null)
            return NotFound();

        var normalizedLabel = NormalizeToken(request.Label);
        if (string.IsNullOrWhiteSpace(normalizedLabel))
            return BadRequest(new { error = "Label non valida." });

        var expectedKind = NormalizeExpectedKind(request.ExpectedKind);
        if (expectedKind == null)
        {
            return BadRequest(new
            {
                error = "expectedKind non supportato.",
                supportedKinds = new[]
                {
                    "person",
                    "place",
                    "team",
                    "organization",
                    "project",
                    "activity",
                    "emotion",
                    "idea",
                    "problem",
                    "finance",
                    "object",
                    "vehicle",
                    "brand",
                    "product_model",
                    "year",
                    "date",
                    "time",
                    "amount",
                    "not_entity",
                    "not_person"
                }
            });
        }

        var scope = $"name:{normalizedLabel}";
        var existing = await _db.PersonalPolicies.FirstOrDefaultAsync(policy =>
            policy.UserId == entry.UserId &&
            policy.PolicyKey == "entity_kind_override" &&
            policy.Scope == scope,
            HttpContext.RequestAborted);

        if (existing == null)
        {
            _db.PersonalPolicies.Add(new PersonalPolicy
            {
                Id = Guid.NewGuid(),
                UserId = entry.UserId,
                PolicyKey = "entity_kind_override",
                PolicyValue = expectedKind,
                Scope = scope,
                Origin = "explicit",
                Confidence = 1.0f,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.PolicyValue = expectedKind;
            existing.Origin = "explicit";
            existing.Confidence = 1.0f;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var noteScope = $"entry:{entry.Id}:name:{normalizedLabel}";
            var existingNote = await _db.PersonalPolicies.FirstOrDefaultAsync(policy =>
                policy.UserId == entry.UserId &&
                policy.PolicyKey == "entity_feedback_note" &&
                policy.Scope == noteScope,
                HttpContext.RequestAborted);

            var safeNote = request.Note.Trim();
            if (safeNote.Length > 350)
                safeNote = safeNote[..350];

            if (existingNote == null)
            {
                _db.PersonalPolicies.Add(new PersonalPolicy
                {
                    Id = Guid.NewGuid(),
                    UserId = entry.UserId,
                    PolicyKey = "entity_feedback_note",
                    PolicyValue = safeNote,
                    Scope = noteScope,
                    Origin = "explicit",
                    Confidence = 1.0f,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existingNote.PolicyValue = safeNote;
                existingNote.Origin = "explicit";
                existingNote.Confidence = 1.0f;
                existingNote.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(HttpContext.RequestAborted);
        await _rebuildQueue.EnqueueAsync(entry.UserId, HttpContext.RequestAborted);

        return Ok(new EntryEntityFeedbackResponse(
            entry.Id,
            request.Label.Trim(),
            expectedKind,
            RebuildQueued: true,
            Message: "Feedback salvato. Ricalcolo memoria avviato in background."
        ));
    }

    private static string? NormalizeExpectedKind(string rawKind)
    {
        if (string.IsNullOrWhiteSpace(rawKind))
            return null;

        var normalized = NormalizeToken(rawKind);
        return normalized switch
        {
            "persona" or "person" => "person",
            "luogo" or "place" or "location" or "citta" or "city" => "place",
            "squadra" or "team" or "club" => "team",
            "organizzazione" or "organization" or "company" => "organization",
            "progetto" or "project" => "project",
            "attivita" or "activity" or "task" or "habit" => "activity",
            "emozione" or "emotion" or "feeling" => "emotion",
            "idea" or "belief" or "philosophy" => "idea",
            "problema" or "problem" or "blocker" => "problem",
            "finanza" or "finance" or "money" => "finance",
            "oggetto" or "object" or "item" or "thing" => "object",
            "veicolo" or "vehicle" or "car" or "auto" or "macchina" or "automobile" => "vehicle",
            "marca" or "brand" => "brand",
            "productmodel" or "modello" or "model" => "product_model",
            "anno" or "year" => "year",
            "data" or "date" => "date",
            "ora" or "orario" or "time" => "time",
            "importo" or "amount" or "prezzo" => "amount",
            "notentity" or "nonentity" or "ignore" or "none" => "not_entity",
            "notperson" or "nonperson" => "not_person",
            _ => null
        };
    }

    private static string NormalizeToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
