using Microsoft.EntityFrameworkCore;
using System.IO;

namespace GreenLumaPresets.Models;

public class AppDbContext : DbContext
{
    public DbSet<Preset> Presets { get; set; }
    public DbSet<AppId> AppIds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppId>().HasIndex(a => new { a.PresetId, a.Position });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Resolve next to the executable so the database does not depend on the working directory
        // (shortcuts with another "Start in" folder, restarts by the updater, ...).
        var databasePath = Path.Combine(AppContext.BaseDirectory, "presets.db");
        optionsBuilder.UseSqlite($"Data Source={databasePath};Mode=ReadWriteCreate");
    }
}
