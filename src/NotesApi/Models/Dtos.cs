using System.ComponentModel.DataAnnotations;

namespace NotesApi.Models;

// ---------- Auth ----------
public record TokenRequest(
    [Required] string UserName,
    [Required] string Password);

public record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAt);

// ---------- Users ----------
public record UserDto(int Id, string UserName, string? Nickname);

public record CreateUserRequest(
    [Required, MaxLength(100)] string UserName,
    [MaxLength(100)] string? Nickname,
    [Required, MinLength(8), MaxLength(128)] string Password);

// Password is optional: leave it null to keep the current password.
public record UpdateUserRequest(
    [Required, MaxLength(100)] string UserName,
    [MaxLength(100)] string? Nickname,
    [MinLength(8), MaxLength(128)] string? Password);

// ---------- Notes ----------
public record NoteDto(int Id, string Note, int UserId, DateTime Timestamp);

// The owner is taken from the access token, not the request body.
public record CreateNoteRequest(
    [Required] string Note);

public record UpdateNoteRequest(
    [Required] string Note);
