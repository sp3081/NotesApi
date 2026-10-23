using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Auth;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/token")]
[AllowAnonymous]
public class TokenController(
    NotesDbContext db,
    TokenService tokens,
    IPasswordHasher<User> hasher) : ControllerBase
{
    // POST api/token   { "userName": "...", "password": "..." }
    // Returns a JWT to send as:  Authorization: Bearer <accessToken>
    [HttpPost]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Create(TokenRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == req.UserName);

        // Same response whether the user is missing or the password is wrong,
        // so callers can't probe which user names exist.
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return Unauthorized(new { message = "Invalid user name or password." });

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid user name or password." });

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, req.Password);
            await db.SaveChangesAsync();
        }

        return Ok(tokens.CreateToken(user));
    }
}
