using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Library.Api.Auth;

public class RoleClaimsTransformation(IConfiguration configuration) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(principal);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var mappings = configuration.GetSection("RoleMappings").Get<Dictionary<string, string>>()
            ?? new Dictionary<string, string>();
        var normalizedMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            normalizedMappings[mapping.Key.Trim().ToLowerInvariant()] = mapping.Value;
        }

        ApplyEmailListRole("ADMIN_EMAILS", "Admin", normalizedMappings);
        ApplyEmailListRole("LIBRARIAN_EMAILS", "Librarian", normalizedMappings);
        ApplyEmailListRole("MEMBER_EMAILS", "Member", normalizedMappings);

        var roleMappingsJson = configuration["ROLE_MAPPINGS_JSON"];
        if (!string.IsNullOrWhiteSpace(roleMappingsJson))
        {
            try
            {
                var envMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(roleMappingsJson);
                if (envMappings is not null)
                {
                    foreach (var mapping in envMappings)
                    {
                        normalizedMappings[mapping.Key.Trim().ToLowerInvariant()] = mapping.Value;
                    }
                }
            }
            catch
            {
                // Keep app running with section-based mappings if env JSON is malformed.
            }
        }

        if (normalizedMappings.TryGetValue(normalizedEmail, out var role)
            && !identity.HasClaim(ClaimTypes.Role, role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return Task.FromResult(principal);
    }

    private void ApplyEmailListRole(string key, string role, IDictionary<string, string> mappings)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var emails = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var email in emails)
        {
            mappings[email.ToLowerInvariant()] = role;
        }
    }
}
