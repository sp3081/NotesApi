using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Controllers;
using NotesApi.Models;
using NotesApi.Tests.Helpers;

namespace NotesApi.Tests.Unit;

public class UsersControllerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly PasswordHasher<User> _hasher = new();

    public void Dispose() => _db.Dispose();

    private UsersController ControllerFor(int userId) =>
        new UsersController(_db.CreateContext(), _hasher).AsUser(userId);

    [Fact]
    public async Task Create_HashesPassword_AndNeverStoresPlainText()
    {
        var controller = new UsersController(_db.CreateContext(), _hasher);

        var result = await controller.Create(new CreateUserRequest("carol", "C", "Sup3r-Secret!"));

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<UserDto>(created.Value);

        using var db = _db.CreateContext();
        var saved = await db.Users.SingleAsync(u => u.Id == dto.Id);
        Assert.NotEqual("Sup3r-Secret!", saved.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(saved, saved.PasswordHash, "Sup3r-Secret!"));
    }

    [Fact]
    public async Task Create_DuplicateUserName_ReturnsConflict()
    {
        _db.AddUser("dave");
        var controller = new UsersController(_db.CreateContext(), _hasher);

        var result = await controller.Create(new CreateUserRequest("dave", null, "Another-Pass1"));

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetNotesForUser_AnotherUser_ReturnsForbid()
    {
        var alice = _db.AddUser("alice");
        var bob = _db.AddUser("bob");

        var result = await ControllerFor(alice).GetNotesForUser(bob);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Update_AnotherUser_ReturnsForbid()
    {
        var alice = _db.AddUser("alice");
        var bob = _db.AddUser("bob");

        var result = await ControllerFor(alice).Update(bob, new UpdateUserRequest("bob", "pwned", null));

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Delete_Self_AlsoDeletesTheirNotes()
    {
        var alice = _db.AddUser("alice");
        var bob = _db.AddUser("bob");
        _db.AddNote(alice, "a1");
        _db.AddNote(alice, "a2");
        _db.AddNote(bob, "b1");

        var result = await ControllerFor(alice).Delete(alice);

        Assert.IsType<NoContentResult>(result);
        using var db = _db.CreateContext();
        Assert.False(await db.Users.AnyAsync(u => u.Id == alice));
        Assert.Equal(0, await db.Notes.CountAsync(n => n.UserId == alice));
        Assert.Equal(1, await db.Notes.CountAsync(n => n.UserId == bob));
    }
}
