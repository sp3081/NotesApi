using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace NotesApi.Auth;

public static class ClaimsPrincipalExtensions
{
    // The user id stored in the token's "sub" claim.
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("Access token has no valid 'sub' claim.");
    }
}
