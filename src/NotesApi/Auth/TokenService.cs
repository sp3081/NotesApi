using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NotesApi.Models;

namespace NotesApi.Auth;

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JsonWebTokenHandler _handler = new();

    public TokenResponse CreateToken(User user)
    {
        var jwt = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(jwt.ExpiresMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(
                CreateSigningKey(jwt.Key), SecurityAlgorithms.HmacSha256)
        };

        return new TokenResponse(_handler.CreateToken(descriptor), "Bearer", expires);
    }

    public static SymmetricSecurityKey CreateSigningKey(string key) =>
        new(Encoding.UTF8.GetBytes(key));
}
