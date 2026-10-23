using Microsoft.AspNetCore.Mvc;
using NotesApi.Controllers;
using NotesApi.Models;
using NotesApi.Tests.Helpers;

namespace NotesApi.Tests.Unit;

public class NotesControllerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly int _alice;
    private readonly int _bob;

    public NotesControllerTests()
    {
        _alice = _db.AddUser("alice");
        _bob = _db.AddUser("bob");
    }

    public void Dispose() => _db.Dispose();

    private NotesController ControllerFor(int userId) =>
        new NotesController(_db.CreateContext()).AsUser(userId);

    // ---------- Create ----------

    [Fact]
    public async Task Create_SavesNoteForCallerWithUtcTimestamp()
    {
        var before = DateTime.UtcNow;

        var result = await ControllerFor(_alice).Create(new CreateNoteRequest("Buy milk"));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<NoteDto>(created.Value);
        Assert.Equal("Buy milk", dto.Note);
        Assert.Equal(_alice, dto.UserId);
        Assert.InRange(dto.Timestamp, before, DateTime.UtcNow);

        using var db = _db.CreateContext();
        var saved = await db.Notes.FindAsync(dto.Id);
        Assert.NotNull(saved);
        Assert.Equal(_alice, saved.UserId);
    }

    // ---------- Read ----------

    [Fact]
    public async Task GetAll_ReturnsOnlyCallersNotes_NewestFirst()
    {
        var older = _db.AddNote(_alice, "older", DateTime.UtcNow.AddHours(-2));
        var newer = _db.AddNote(_alice, "newer", DateTime.UtcNow.AddHours(-1));
        _db.AddNote(_bob, "bob's note");

        var result = await ControllerFor(_alice).GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var notes = Assert.IsAssignableFrom<IEnumerable<NoteDto>>(ok.Value).ToList();
        Assert.Equal(new[] { newer, older }, notes.Select(n => n.Id));
        Assert.All(notes, n => Assert.Equal(_alice, n.UserId));
    }

    [Fact]
    public async Task GetAll_WithNoNotes_ReturnsEmptyList()
    {
        var result = await ControllerFor(_alice).GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<NoteDto>>(ok.Value));
    }

    [Fact]
    public async Task GetById_OwnNote_ReturnsNote()
    {
        var id = _db.AddNote(_alice, "hello");

        var result = await ControllerFor(_alice).GetById(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<NoteDto>(ok.Value);
        Assert.Equal("hello", dto.Note);
    }

    [Fact]
    public async Task GetById_OtherUsersNote_ReturnsNotFound()
    {
        var bobsNote = _db.AddNote(_bob, "private");

        var result = await ControllerFor(_alice).GetById(bobsNote);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetById_MissingNote_ReturnsNotFound()
    {
        var result = await ControllerFor(_alice).GetById(9999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Update ----------

    [Fact]
    public async Task Update_OwnNote_ChangesTextAndRefreshesTimestamp()
    {
        var oldTime = DateTime.UtcNow.AddDays(-1);
        var id = _db.AddNote(_alice, "draft", oldTime);

        var result = await ControllerFor(_alice).Update(id, new UpdateNoteRequest("final"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<NoteDto>(ok.Value);
        Assert.Equal("final", dto.Note);
        Assert.True(dto.Timestamp > oldTime);

        using var db = _db.CreateContext();
        Assert.Equal("final", (await db.Notes.FindAsync(id))!.Content);
    }

    [Fact]
    public async Task Update_OtherUsersNote_ReturnsNotFoundAndLeavesItUnchanged()
    {
        var bobsNote = _db.AddNote(_bob, "original");

        var result = await ControllerFor(_alice).Update(bobsNote, new UpdateNoteRequest("hacked"));

        Assert.IsType<NotFoundResult>(result.Result);
        using var db = _db.CreateContext();
        Assert.Equal("original", (await db.Notes.FindAsync(bobsNote))!.Content);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_OwnNote_RemovesIt()
    {
        var id = _db.AddNote(_alice, "temp");

        var result = await ControllerFor(_alice).Delete(id);

        Assert.IsType<NoContentResult>(result);
        using var db = _db.CreateContext();
        Assert.Null(await db.Notes.FindAsync(id));
    }

    [Fact]
    public async Task Delete_OtherUsersNote_ReturnsNotFoundAndKeepsIt()
    {
        var bobsNote = _db.AddNote(_bob, "keep me");

        var result = await ControllerFor(_alice).Delete(bobsNote);

        Assert.IsType<NotFoundResult>(result);
        using var db = _db.CreateContext();
        Assert.NotNull(await db.Notes.FindAsync(bobsNote));
    }
}
