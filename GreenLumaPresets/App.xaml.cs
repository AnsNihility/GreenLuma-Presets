using GreenLumaPresets.Controllers;
using GreenLumaPresets.Logging;
using GreenLumaPresets.Models;
using GreenLumaPresets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        this.Services = ConfigureServices(loggerFactory);
        this.Settings = Configuration.GetSection("Settings").Get<Settings>()
            ?? new Settings();

        this.InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for updates on startup (silently, without "no update" message)
        var updateService = Services.GetService<UpdateService>();
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

    private static IServiceProvider ConfigureServices(ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>();
        services.AddTransient<PresetsService>();
        services.AddTransient<GreenLumaService>();
        services.AddTransient<SteamService>();
        services.AddTransient<UpdateService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton(loggerFactory);
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
