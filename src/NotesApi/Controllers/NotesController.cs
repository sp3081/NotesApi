using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotesApi.Auth;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Controllers;

// Every endpoint works only on the notes of the user identified by the token.
// Another user's note returns 404, so note ids from other users aren't revealed.
[ApiController]
[Route("api/notes")]
[Authorize]
public class NotesController(NotesDbContext db) : ControllerBase
{
    // GET api/notes   -> your notes, newest first
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteDto>>> GetAll()
    {
        var userId = User.GetUserId();

        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Timestamp)
            .Select(n => new NoteDto(n.Id, n.Content, n.UserId, n.Timestamp))
            .ToListAsync();
        return Ok(notes);
    }

    // GET api/notes/10
    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoteDto>> GetById(int id)
    {
        var userId = User.GetUserId();
        var n = await db.Notes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        return n is null ? NotFound() : Ok(new NoteDto(n.Id, n.Content, n.UserId, n.Timestamp));
    }

    // POST api/notes   { "note": "..." }
    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteRequest req)
    {
        var note = new Note
        {
            Content = req.Note,
            UserId = User.GetUserId(),
            Timestamp = DateTime.UtcNow
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        var dto = new NoteDto(note.Id, note.Content, note.UserId, note.Timestamp);
        return CreatedAtAction(nameof(GetById), new { id = note.Id }, dto);
    }

    // PUT api/notes/10   (updates text and refreshes the timestamp)
    [HttpPut("{id:int}")]
    public async Task<ActionResult<NoteDto>> Update(int id, UpdateNoteRequest req)
    {
        var userId = User.GetUserId();
        var note = await db.Notes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (note is null) return NotFound();

        note.Content = req.Note;
        note.Timestamp = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new NoteDto(note.Id, note.Content, note.UserId, note.Timestamp));
    }

    // DELETE api/notes/10
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.GetUserId();
        var note = await db.Notes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (note is null) return NotFound();

        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
