namespace NotesApi.Auth;

// Bound from the "Jwt" section of appsettings.json.
public class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // HMAC-SHA256 signing key; must be at least 32 bytes.
    // Keep the real one in user-secrets / environment variables, not in source control.
    public string Key { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 60;
}
