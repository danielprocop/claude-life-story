using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DiarioIntelligente.API.Controllers;

public abstract class AdminAuthenticatedController : AuthenticatedController
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN",
        "DEV",
        "ANNOTATOR"
    };

    protected ActionResult? EnsureAdminRole(out string role)
    {
        // Temporary rollout mode: all authenticated users can use admin feedback tools.
        // Set Admin__AllowAllUsers=false to restore strict role checks.
        if (AllowAllUsersAsAdmin())
        {
            role = "ADMIN";
            return null;
        }

        role = ResolveRoleFromClaims();
        if (!string.IsNullOrWhiteSpace(role))
            return null;

        var email = GetUserEmail();
        if (string.Equals(email, "demo@diariointelligente.app", StringComparison.OrdinalIgnoreCase))
        {
            role = "DEV";
            return null;
        }

        var allowedEmailList = Environment.GetEnvironmentVariable("Admin__AllowedEmails");
        if (!string.IsNullOrWhiteSpace(allowedEmailList))
        {
            var allowed = allowedEmailList
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (allowed.Contains(email))
            {
                role = "ADMIN";
                return null;
            }
        }

        return Forbid();
    }

    private static bool AllowAllUsersAsAdmin()
    {
        var raw = Environment.GetEnvironmentVariable("Admin__AllowAllUsers");
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        return bool.TryParse(raw, out var parsed) && parsed;
    }

    private string ResolveRoleFromClaims()
    {
        foreach (var claim in User.Claims.Where(x =>
                     x.Type == ClaimTypes.Role ||
                     x.Type == "role" ||
                     x.Type == "cognito:groups" ||
                     x.Type == "groups"))
        {
            foreach (var token in ParseRoleTokens(claim.Value))
            {
                var normalized = token.Trim().Trim('"').ToUpperInvariant();
                if (AllowedRoles.Contains(normalized))
                    return normalized;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> ParseRoleTokens(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (values != null)
                    return values;
            }
            catch
            {
                // fallback split below
            }
        }

        return trimmed.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

