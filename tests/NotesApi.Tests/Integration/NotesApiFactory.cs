using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotesApi.Data;

namespace NotesApi.Tests.Integration;

// Runs the real API in memory (no network port, no SQL Server needed).
// Everything is the production pipeline - routing, JWT auth, validation -
// except the database, which is an in-memory SQLite database.
public class NotesApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=True");

    public NotesApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");   // Program.cs skips SQL Server in this environment
        builder.ConfigureServices(services =>
            services.AddDbContext<NotesDbContext>(o => o.UseSqlite(_connection)));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<NotesDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
