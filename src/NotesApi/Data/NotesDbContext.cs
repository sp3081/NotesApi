using Microsoft.EntityFrameworkCore;
using NotesApi.Models;

namespace NotesApi.Data;

public class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.Property(u => u.UserName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Nickname).HasMaxLength(100);
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.UserName).IsUnique();
        });

        modelBuilder.Entity<Note>(e =>
        {
            e.ToTable("Notes");
            e.HasKey(n => n.Id);
            e.Property(n => n.Content).HasColumnName("Note").IsRequired();
            // SQL Server-specific column type and default; skipped when the
            // tests run against SQLite, which has neither.
            if (Database.IsSqlServer())
            {
                e.Property(n => n.Timestamp).HasColumnType("datetime2(3)")
                                            .HasDefaultValueSql("SYSUTCDATETIME()");
            }
            e.HasOne(n => n.User)
             .WithMany(u => u.Notes)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.UserId, n.Timestamp });
        });
    }
}
