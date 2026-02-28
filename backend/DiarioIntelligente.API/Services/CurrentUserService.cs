using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.API.Services;

public sealed class CurrentUserService
{
    private static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string DemoUserEmail = "demo@diariointelligente.app";

    private readonly AppDbContext _db;

    public CurrentUserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CurrentUserContext> EnsureUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userContext = ResolveUser(principal);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userContext.UserId, cancellationToken);
        if (user == null)
        {
            user = new Core.Models.User
            {
                Id = userContext.UserId,
                Email = userContext.Email,
                PasswordHash = userContext.IsAuthenticated ? "cognito" : "demo",
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);
            return userContext;
        }

        if (!string.Equals(user.Email, userContext.Email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = userContext.Email;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return userContext;
    }

    public CurrentUserContext ResolveUser(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");

        if (principal.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(sub))
        {
            return new CurrentUserContext(
                ParseOrHashGuid(sub),
                string.IsNullOrWhiteSpace(email) ? $"user-{sub}@cognito.local" : email,
                true);
        }

        return new CurrentUserContext(DemoUserId, DemoUserEmail, false);
    }

    private static Guid ParseOrHashGuid(string value)
    {
        if (Guid.TryParse(value, out var guid))
            return guid;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
