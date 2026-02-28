using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GoalItemsController : AuthenticatedController
{
    private readonly IGoalItemRepository _goalRepo;
    private readonly ISearchProjectionService _searchProjectionService;

    public GoalItemsController(IGoalItemRepository goalRepo, ISearchProjectionService searchProjectionService)
    {
        _goalRepo = goalRepo;
        _searchProjectionService = searchProjectionService;
    }

    [HttpGet]
    public async Task<ActionResult<List<GoalItemResponse>>> GetAll()
    {
        var goals = await _goalRepo.GetRootGoalsAsync(GetUserId());
        return Ok(goals.Select(MapGoalItem).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GoalItemResponse>> GetById(Guid id)
    {
        var goal = await _goalRepo.GetByIdAsync(id, GetUserId());
        if (goal == null) return NotFound();
        return Ok(MapGoalItem(goal));
    }

    [HttpPost]
    public async Task<ActionResult<GoalItemResponse>> Create([FromBody] CreateGoalRequest request)
    {
        var userId = GetUserId();
        var goal = new GoalItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            ParentGoalId = request.ParentGoalId,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };

        await _goalRepo.CreateAsync(goal);
        await _searchProjectionService.ProjectGoalItemAsync(goal, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetById), new { id = goal.Id }, MapGoalItem(goal));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GoalItemResponse>> Update(Guid id, [FromBody] UpdateGoalRequest request)
    {
        var goal = await _goalRepo.GetByIdAsync(id, GetUserId());
        if (goal == null) return NotFound();

        if (request.Title != null) goal.Title = request.Title;
        if (request.Status != null)
        {
            goal.Status = request.Status;
            if (request.Status == "completed")
                goal.CompletedAt = DateTime.UtcNow;
        }

        await _goalRepo.UpdateAsync(goal);
        await _searchProjectionService.ProjectGoalItemAsync(goal, HttpContext.RequestAborted);
        return Ok(MapGoalItem(goal));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var goal = await _goalRepo.GetByIdAsync(id, GetUserId());
        if (goal == null) return NotFound();

        await _goalRepo.DeleteAsync(id);
        await _searchProjectionService.DeleteGoalItemAsync(id, goal.UserId, HttpContext.RequestAborted);
        return NoContent();
    }

    private static GoalItemResponse MapGoalItem(GoalItem g) => new(
        g.Id, g.Title, g.Description, g.Status, g.CreatedAt, g.CompletedAt, g.ParentGoalId,
        g.SubGoals?.Select(MapGoalItem).ToList() ?? new List<GoalItemResponse>()
    );
}
