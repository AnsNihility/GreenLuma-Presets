using FluentResults;
using GreenLumaPresets.Services;

namespace GreenLumaPresets.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public List<(string Kind, string Title, string Message)> Shown { get; } = [];

    /// <summary>
    /// Answers given to the next Confirm calls; true once empty.
    /// </summary>
    public Queue<bool> Answers { get; } = new();

    public void ShowInformation(string title, string message) => Shown.Add(("Information", title, message));
    public void ShowWarning(string title, string message) => Shown.Add(("Warning", title, message));
    public void ShowError(string title, string message) => Shown.Add(("Error", title, message));

    public bool Confirm(string title, string message)
    {
        Shown.Add(("Question", title, message));
        return Answers.Count == 0 || Answers.Dequeue();
    }
}

internal sealed class FakeGreenLumaService : IGreenLumaService
{
    public List<string> Calls { get; } = [];

    public bool Installed { get; set; }
    public bool DeleteCacheExeInstalled { get; set; }
    public Result LoadAppListResult { get; set; } = Result.Ok();
    public Result RestartSteamResult { get; set; } = Result.Ok();
    public Result ExclusionResult { get; set; } = Result.Ok();
    public Result InstallResult { get; set; } = Result.Ok();
    public Result UninstallResult { get; set; } = Result.Ok();

    public bool IsSteamFound => true;
    public bool IsGreenLumaInstalled() => Installed;
    public bool IsDeleteCacheExeInstalled() => DeleteCacheExeInstalled;

    public Result LoadAppList(Guid presetId) { Calls.Add(nameof(LoadAppList)); return LoadAppListResult; }
    public Result ClearAppList() { Calls.Add(nameof(ClearAppList)); return Result.Ok(); }
    public Result RestartSteam() { Calls.Add(nameof(RestartSteam)); return RestartSteamResult; }
    public void DeleteSteamAppCache() => Calls.Add(nameof(DeleteSteamAppCache));

    public Task<Result> InstallGreenLumaFromArchiveAsync(string pathToArchive, string? expectedSha256 = null)
    {
        Calls.Add(nameof(InstallGreenLumaFromArchiveAsync));
        if (InstallResult.IsSuccess) Installed = true;
        return Task.FromResult(InstallResult);
    }

    public Task<Result> InstallGreenLumaFromUrlAsync(string greenLumaFilesUrl, string? expectedSha256 = null)
    {
        Calls.Add(nameof(InstallGreenLumaFromUrlAsync));
        if (InstallResult.IsSuccess) Installed = true;
        return Task.FromResult(InstallResult);
    }

    public Result UninstallGreenLuma()
    {
        Calls.Add(nameof(UninstallGreenLuma));
        if (UninstallResult.IsSuccess) Installed = false;
        return UninstallResult;
    }

    public Task<Result> AddDefenderExclusionAsync()
    {
        Calls.Add(nameof(AddDefenderExclusionAsync));
        return Task.FromResult(ExclusionResult);
    }
}

internal sealed class FakeImportFromSteamDialog : IImportFromSteamDialog
{
    /// <summary>
    /// What the dialog returns; null simulates a cancellation.
    /// </summary>
    public SteamApp? Next { get; set; }

    public SteamApp? Show() => Next;
}

internal sealed class FakeClipboardService : IClipboardService
{
    public string Text { get; set; } = string.Empty;

    public string GetText() => Text;
}

internal sealed class FakeUpdateService : IUpdateService
{
    public int Checks { get; private set; }

    public void CheckForUpdates(bool showNoUpdateMessage = false) => Checks++;
    public string GetCurrentVersion() => "1.2.3.4";
}

internal sealed class FakeSteamService : ISteamService
{
    public List<int> RequestedAppIds { get; } = [];
    public Result<SteamApp> AppData { get; set; } = Result.Fail("not configured");
    public Result<List<SteamSearchResult>> SearchResults { get; set; } = Result.Ok(new List<SteamSearchResult>());

    public Task<Result<SteamApp>> GetAppData(int appId)
    {
        RequestedAppIds.Add(appId);
        return Task.FromResult(AppData);
    }

    public Task<Result<List<SteamSearchResult>>> SearchAppsByName(string name) => Task.FromResult(SearchResults);
}
