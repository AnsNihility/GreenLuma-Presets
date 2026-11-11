using GreenLumaPresets.Logging;
using GreenLumaPresets.Models;
using GreenLumaPresets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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

    private readonly ILogger logger;

    public App()
    {
        var loggerFactory = LoggerFactory.Create(ConfigureLogging);
        logger = loggerFactory.CreateLogger<App>();
        RegisterGlobalExceptionHandlers();

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        this.Configuration = builder.Build();
        this.Settings = Configuration.GetSection("Settings").Get<Settings>()
            ?? new Settings();
        this.Services = ConfigureServices(loggerFactory, Settings);

        this.InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for updates on startup (silently, without "no update" message)
        var updateService = Services.GetService<IUpdateService>();
        updateService?.CheckForUpdates(showNoUpdateMessage: false);
    }

    private static void ConfigureLogging(ILoggingBuilder builder) => builder
        .SetMinimumLevel(LogLevel.Information)
        .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning)
        .AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultDirectory));

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            logger.LogCritical(e.ExceptionObject as Exception, "Fatal unhandled exception");
            MessageBox.Show(
                $"A fatal error occurred and GreenLuma Presets must close.\n\n{(e.ExceptionObject as Exception)?.Message}\n\nDetails were written to {FileLoggerProvider.DefaultDirectory}",
                "GreenLuma Presets",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        logger.LogError(e.Exception, "Unhandled UI exception");

        // A failed handler may have left the wait cursor on.
        Mouse.OverrideCursor = null;

        Services?.GetService<IDialogService>()?.ShowError(
            "Unexpected error",
            $"{e.Exception.Message}\n\nDetails were written to {FileLoggerProvider.DefaultDirectory}");

        e.Handled = true;
    }

    /// <summary>
    /// Versions up to 1.3.x stored the database next to the executable: copy it once to the per-user folder.
    /// The original file is kept as a backup.
    /// </summary>
    private static void MoveLegacyDatabase(ILogger logger)
    {
        if (File.Exists(AppPaths.DatabasePath) || !File.Exists(AppPaths.LegacyDatabasePath)) return;

        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.Copy(AppPaths.LegacyDatabasePath, AppPaths.DatabasePath);
        logger.LogInformation("Copied the presets database from {Legacy} to {Current}",
            AppPaths.LegacyDatabasePath, AppPaths.DatabasePath);
    }

    private static IServiceProvider ConfigureServices(ILoggerFactory loggerFactory, Settings settings)
    {
        var services = new ServiceCollection();

        services.AddSingleton(settings);
        services.AddDbContext<AppDbContext>();
        services.AddTransient<IPresetsService, PresetsService>();
        services.AddTransient<IGreenLumaService, GreenLumaService>();
        services.AddTransient<ISteamService, SteamService>();
        services.AddTransient<IUpdateService, UpdateService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton(loggerFactory);
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        MoveLegacyDatabase(loggerFactory.CreateLogger<App>());

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();
        }

        return serviceProvider;
    }
}
