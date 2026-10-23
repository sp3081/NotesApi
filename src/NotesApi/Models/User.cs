namespace NotesApi.Models;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Nickname { get; set; }

    // Hashed with ASP.NET Core Identity's PasswordHasher (PBKDF2). Never returned by the API.
    public string PasswordHash { get; set; } = string.Empty;

    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
