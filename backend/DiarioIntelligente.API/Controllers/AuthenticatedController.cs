using System.Security.Claims;
using DiarioIntelligente.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

[Authorize]
[ApiController]
public abstract class AuthenticatedController : ControllerBase
{
    private static readonly Guid FallbackDemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string FallbackDemoEmail = "demo@diariointelligente.app";

    protected Guid GetUserId()
    {
        if (HttpContext.Items.TryGetValue(CurrentUserContext.HttpContextItemKey, out var value) &&
            value is CurrentUserContext currentUser)
            return currentUser.UserId;

        return FallbackDemoUserId;
    }

    protected string GetUserEmail()
    {
        if (HttpContext.Items.TryGetValue(CurrentUserContext.HttpContextItemKey, out var value) &&
            value is CurrentUserContext currentUser)
            return currentUser.Email;

        return User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? FallbackDemoEmail;
    }
}
