using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : AuthenticatedController
{
    private readonly IPersonalModelService _personalModelService;

    public ProfileController(IPersonalModelService personalModelService)
    {
        _personalModelService = personalModelService;
    }

    [HttpGet]
    public async Task<ActionResult<PersonalModelResponse>> Get()
    {
        var profile = await _personalModelService.BuildAsync(GetUserId(), HttpContext.RequestAborted);
        return Ok(profile);
    }
}
