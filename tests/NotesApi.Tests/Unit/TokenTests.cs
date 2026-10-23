using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NotesApi.Auth;
using NotesApi.Controllers;
using NotesApi.Models;
using NotesApi.Tests.Helpers;

namespace NotesApi.Tests.Unit;

public class TokenTests : IDisposable
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Key = "unit-test-signing-key-that-is-definitely-long-enough",
        ExpiresMinutes = 30
    };

    private readonly TestDb _db = new();
    private readonly TokenService _tokens = new(Options.Create(Jwt));

    public void Dispose() => _db.Dispose();

    private TokenController Controller() =>
        new(_db.CreateContext(), _tokens, new PasswordHasher<User>());

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidIssuer = Jwt.Issuer,
        ValidAudience = Jwt.Audience,
        IssuerSigningKey = TokenService.CreateSigningKey(key)
    };

    // ---------- TokenService ----------

    [Fact]
    public async Task CreateToken_ContainsUserIdAndName_AndValidates()
    {
        var user = new User { Id = 42, UserName = "alice" };

        var response = _tokens.CreateToken(user);

        Assert.Equal("Bearer", response.TokenType);
        var result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(response.AccessToken, ValidationParameters(Jwt.Key));
        Assert.True(result.IsValid);
        Assert.Equal("42", result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal("alice", result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value);
    }

    [Fact]
    public void CreateToken_ExpiresAfterConfiguredMinutes()
    {
        var response = _tokens.CreateToken(new User { Id = 1, UserName = "a" });

        var expected = DateTime.UtcNow.AddMinutes(Jwt.ExpiresMinutes);
        Assert.InRange(response.ExpiresAt, expected.AddSeconds(-5), expected.AddSeconds(5));
    }

    [Fact]
    public async Task Token_SignedWithDifferentKey_IsRejected()
    {
        var response = _tokens.CreateToken(new User { Id = 1, UserName = "a" });

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            response.AccessToken,
            ValidationParameters("a-completely-different-key-also-32-bytes-or-more!!"));

        Assert.False(result.IsValid);
    }

    // ---------- TokenController ----------

    [Fact]
    public async Task Login_CorrectPassword_ReturnsToken()
    {
        _db.AddUser("alice");

        var result = await Controller().Create(new TokenRequest("alice", TestDb.Password));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var token = Assert.IsType<TokenResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        _db.AddUser("alice");

        var result = await Controller().Create(new TokenRequest("alice", "wrong-password"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsUnauthorized()
    {
        var result = await Controller().Create(new TokenRequest("nobody", TestDb.Password));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
