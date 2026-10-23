namespace NotesApi.Models;

public class Note
{
    public int Id { get; set; }

    // Named "Content" in C# because a property can't share its class name;
    // it maps to the "Note" column in SQL Server (see NotesDbContext).
    public string Content { get; set; } = string.Empty;

    public int UserId { get; set; }
    public DateTime Timestamp { get; set; }

    public User? User { get; set; }
}
