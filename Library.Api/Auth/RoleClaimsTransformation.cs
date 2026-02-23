using System.Security.Claims;
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

        if (normalizedMappings.TryGetValue(normalizedEmail, out var role)
            && !identity.HasClaim(ClaimTypes.Role, role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return Task.FromResult(principal);
    }
}
