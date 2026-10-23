using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Tests.Helpers;

// A fresh in-memory SQLite database per test. Unlike EF's InMemory provider,
// SQLite enforces foreign keys, unique indexes and cascade deletes,
// so it behaves much more like SQL Server.
public sealed class TestDb : IDisposable
{
    public const string Password = "Correct-Horse-1";

    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");

    public TestDb()
    {
        _connection.Open();   // the database lives as long as this connection stays open
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    // Use a new context for each step (arrange / act / assert) so assertions
    // read from the database, not from EF's change tracker.
    public NotesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NotesDbContext>().UseSqlite(_connection).Options);

    public int AddUser(string userName, string password = Password)
    {
        using var db = CreateContext();
        var user = new User { UserName = userName, Nickname = userName + "-nick" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    public int AddNote(int userId, string text, DateTime? timestamp = null)
    {
        using var db = CreateContext();
        var note = new Note { UserId = userId, Content = text, Timestamp = timestamp ?? DateTime.UtcNow };
        db.Notes.Add(note);
        db.SaveChanges();
        return note.Id;
    }

    public void Dispose() => _connection.Dispose();
}
