using Microsoft.EntityFrameworkCore;

namespace GreenLumaPresets.Models;

public class AppDbContext : DbContext
{
    public DbSet<Preset> Presets { get; set; }
    public DbSet<AppId> AppIds { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=presets.db;Mode=ReadWriteCreate");
    }
}
