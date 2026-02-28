namespace DiarioIntelligente.API.Services;

public sealed record CurrentUserContext(Guid UserId, string Email, bool IsAuthenticated)
{
    public const string HttpContextItemKey = "__CurrentUserContext";
}
