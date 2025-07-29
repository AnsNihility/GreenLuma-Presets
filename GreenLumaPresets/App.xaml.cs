using GreenLumaPresets.Controllers;
using GreenLumaPresets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace GreenLumaPresets;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public new static App Current => (App)Application.Current;
    public IServiceProvider Services { get; }
    public IConfiguration Configuration { get; set; }
    public Settings Settings { get; private set; }

    public App()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        this.Configuration = builder.Build();
        this.Services = ConfigureServices();
        this.Settings = Configuration.GetSection("Settings").Get<Settings>()
            ?? throw new ArgumentException("Settings not found in configuration");

        this.InitializeComponent();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>();
        services.AddTransient<PresetsService>();
        services.AddTransient<GreenLumaService>();
        services.AddTransient<SteamService>();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();
        }

        return serviceProvider;
    }
}
