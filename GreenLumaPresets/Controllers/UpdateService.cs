using AutoUpdaterDotNET;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace GreenLumaPresets.Controllers;

public class UpdateService
{
    private readonly ILogger<UpdateService> logger;
    private const string UpdateXmlUrl = "https://raw.githubusercontent.com/AnsNihility/GreenLuma-Presets/main/update.xml";

    public UpdateService(ILogger<UpdateService> logger)
    {
        this.logger = logger;
    }

    public void CheckForUpdates(bool showNoUpdateMessage = false)
    {
        try
        {
            AutoUpdater.ShowSkipButton = true;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
            AutoUpdater.RemindLaterAt = 1;
            AutoUpdater.ReportErrors = showNoUpdateMessage;
            AutoUpdater.RunUpdateAsAdmin = true;
            
            logger.LogInformation("Checking for updates from {Url}", UpdateXmlUrl);
            AutoUpdater.Start(UpdateXmlUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for updates");
        }
    }

    public string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "Unknown";
    }
}
