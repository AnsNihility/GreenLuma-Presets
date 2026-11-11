using Microsoft.EntityFrameworkCore;
using System.IO;

namespace GreenLumaPresets.Models;

public class AppDbContext : DbContext
{
    public DbSet<Preset> Presets { get; set; }
    public DbSet<AppId> AppIds { get; set; }

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppId>().HasIndex(a => new { a.PresetId, a.Position });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;

        // Per-user folder: writable without administrator rights and independent of the working directory.
        Directory.CreateDirectory(AppPaths.DataDirectory);
        optionsBuilder.UseSqlite($"Data Source={AppPaths.DatabasePath};Mode=ReadWriteCreate");
    }
}
