using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotesApi.Data;
using NotesApi.Models;

namespace NotesApi.Tests.Integration;

// End-to-end tests over HTTP. All tests in this class share one API instance
// and database, so each test registers its own uniquely named users.
public class NotesApiTests(NotesApiFactory factory) : IClassFixture<NotesApiFactory>
{
    private const string Password = "Integration-Pass1";

    // Registers a new user, logs in, and returns a client that sends their token.
    private async Task<(HttpClient Client, int UserId)> LoggedInUserAsync()
    {
        var client = factory.CreateClient();
        var userName = "user_" + Guid.NewGuid().ToString("N")[..10];

        var register = await client.PostAsJsonAsync("/api/users",
            new { userName, nickname = "Tester", password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = await register.Content.ReadFromJsonAsync<UserDto>();

        var login = await client.PostAsJsonAsync("/api/token", new { userName, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return (client, user!.Id);
    }

    private static async Task<NoteDto> CreateNoteAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/api/notes", new { note = text });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    // ---------- Authentication ----------

    [Theory]
    [InlineData("GET", "/api/notes")]
    [InlineData("GET", "/api/notes/1")]
    [InlineData("POST", "/api/notes")]
    [InlineData("PUT", "/api/notes/1")]
    [InlineData("DELETE", "/api/notes/1")]
    [InlineData("GET", "/api/users")]
    public async Task Endpoints_WithoutToken_Return401(string method, string url)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { note = "x" });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var (client, _) = await LoggedInUserAsync();
        var me = await (await client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<UserDto>();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/token",
            new { userName = me!.UserName, password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_Returns401()
    {
        var (client, _) = await LoggedInUserAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var tampered = token[..^4] + (token.EndsWith("AAAA") ? "BBBB" : "AAAA");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- CRUD ----------

    [Fact]
    public async Task Notes_FullCrudLifecycle()
    {
        var (client, userId) = await LoggedInUserAsync();

        // Create
        var create = await client.PostAsJsonAsync("/api/notes", new { note = "Buy milk" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = await create.Content.ReadFromJsonAsync<NoteDto>();
        Assert.Equal("Buy milk", created!.Note);
        Assert.Equal(userId, created.UserId);

        // Read one
        var fetched = await client.GetFromJsonAsync<NoteDto>($"/api/notes/{created.Id}");
        Assert.Equal("Buy milk", fetched!.Note);

        // Read list
        var list = await client.GetFromJsonAsync<List<NoteDto>>("/api/notes");
        Assert.Contains(list!, n => n.Id == created.Id);

        // Update
        var update = await client.PutAsJsonAsync($"/api/notes/{created.Id}", new { note = "Buy oat milk" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<NoteDto>();
        Assert.Equal("Buy oat milk", updated!.Note);
        Assert.True(updated.Timestamp >= created.Timestamp);

        // Delete
        var delete = await client.DeleteAsync($"/api/notes/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await client.GetAsync($"/api/notes/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task GetNotes_ReturnsNewestFirst()
    {
        var (client, _) = await LoggedInUserAsync();
        var first = await CreateNoteAsync(client, "first");
        await Task.Delay(20);
        var second = await CreateNoteAsync(client, "second");

        var list = await client.GetFromJsonAsync<List<NoteDto>>("/api/notes");

        Assert.Equal(new[] { second.Id, first.Id }, list!.Select(n => n.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData((string?)null)]
    public async Task CreateNote_WithoutText_Returns400(string? text)
    {
        var (client, _) = await LoggedInUserAsync();

        var response = await client.PostAsJsonAsync("/api/notes", new { note = text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrDelete_MissingNote_Returns404()
    {
        var (client, _) = await LoggedInUserAsync();

        var update = await client.PutAsJsonAsync("/api/notes/987654", new { note = "x" });
        var delete = await client.DeleteAsync("/api/notes/987654");

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    // ---------- Isolation between users ----------

    [Fact]
    public async Task Notes_AreInvisibleAndUntouchableByOtherUsers()
    {
        var (alice, _) = await LoggedInUserAsync();
        var (bob, bobId) = await LoggedInUserAsync();
        var alicesNote = await CreateNoteAsync(alice, "alice's secret");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/notes/{alicesNote.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await bob.PutAsJsonAsync($"/api/notes/{alicesNote.Id}", new { note = "bob was here" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/notes/{alicesNote.Id}")).StatusCode);

        var bobsList = await bob.GetFromJsonAsync<List<NoteDto>>("/api/notes");
        Assert.DoesNotContain(bobsList!, n => n.Id == alicesNote.Id);
        Assert.All(bobsList!, n => Assert.Equal(bobId, n.UserId));

        // Alice's note is unchanged.
        var stillThere = await alice.GetFromJsonAsync<NoteDto>($"/api/notes/{alicesNote.Id}");
        Assert.Equal("alice's secret", stillThere!.Note);
    }

    [Fact]
    public async Task UserNotesRoute_ForAnotherUser_Returns403()
    {
        var (alice, aliceId) = await LoggedInUserAsync();
        var (bob, _) = await LoggedInUserAsync();
        await CreateNoteAsync(alice, "mine");

        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/users/{aliceId}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync($"/api/users/{aliceId}/notes")).StatusCode);
    }

    [Fact]
    public async Task DeletingUser_DeletesTheirNotes()
    {
        var (client, userId) = await LoggedInUserAsync();
        await CreateNoteAsync(client, "one");
        await CreateNoteAsync(client, "two");

        var response = await client.DeleteAsync($"/api/users/{userId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
        Assert.Equal(0, await db.Notes.CountAsync(n => n.UserId == userId));
    }
}
