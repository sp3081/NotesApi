using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Auth;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]   // every endpoint needs a valid token, except registration below
public class UsersController(NotesDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    // GET api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserDto(u.Id, u.UserName, u.Nickname))
            .ToListAsync();
        return Ok(users);
    }

    // GET api/users/me   -> the user the token belongs to
    [HttpGet("me")]
    public Task<ActionResult<UserDto>> GetMe() => GetById(User.GetUserId());

    // GET api/users/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return u is null ? NotFound() : Ok(new UserDto(u.Id, u.UserName, u.Nickname));
    }

    // GET api/users/5/notes   (only your own notes)
    [HttpGet("{id:int}/notes")]
    public async Task<ActionResult<IEnumerable<NoteDto>>> GetNotesForUser(int id)
    {
        if (id != User.GetUserId()) return Forbid();

        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.UserId == id)
            .OrderByDescending(n => n.Timestamp)
            .Select(n => new NoteDto(n.Id, n.Content, n.UserId, n.Timestamp))
            .ToListAsync();
        return Ok(notes);
    }

    // POST api/users   -> registration, no token needed
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest req)
    {
        if (await db.Users.AnyAsync(u => u.UserName == req.UserName))
            return Conflict(new { message = $"UserName '{req.UserName}' is already taken." });

        var user = new User { UserName = req.UserName, Nickname = req.Nickname };
        user.PasswordHash = hasher.HashPassword(user, req.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = new UserDto(user.Id, user.UserName, user.Nickname);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, dto);
    }

    // PUT api/users/5   (only yourself)
    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest req)
    {
        if (id != User.GetUserId()) return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (await db.Users.AnyAsync(u => u.UserName == req.UserName && u.Id != id))
            return Conflict(new { message = $"UserName '{req.UserName}' is already taken." });

        user.UserName = req.UserName;
        user.Nickname = req.Nickname;
        if (!string.IsNullOrEmpty(req.Password))
            user.PasswordHash = hasher.HashPassword(user, req.Password);

        await db.SaveChangesAsync();
        return Ok(new UserDto(user.Id, user.UserName, user.Nickname));
    }

    // DELETE api/users/5   (only yourself; also deletes your notes via cascade)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id != User.GetUserId()) return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
