using FluentResults;
using GreenLumaPresets.Services;
using GreenLumaPresets.ViewModels;

namespace GreenLumaPresets.Tests;

public class ImportFromSteamViewModelTests
{
    private readonly FakeSteamService steam = new();
    private readonly ImportFromSteamViewModel viewModel;

    public ImportFromSteamViewModelTests()
    {
        viewModel = new ImportFromSteamViewModel(steam);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    public async Task Confirm_InvalidAppId_ShowsAnErrorWithoutCallingSteam(string appId)
    {
        viewModel.AppIdText = appId;

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("The provided AppID is not valid", viewModel.ErrorMessage);
        Assert.Empty(steam.RequestedAppIds);
        Assert.Null(viewModel.Result);
    }

    [Fact]
    public async Task Confirm_ValidAppId_SetsTheResultAndRequestsClosing()
    {
        steam.AppData = Result.Ok(new SteamApp("Portal", ["400", "323170"]));
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;
        viewModel.AppIdText = " 400 ";

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal([400], steam.RequestedAppIds);
        Assert.Equal("Portal", viewModel.Result?.Name);
        Assert.True(closeRequested);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Confirm_SteamError_IsShownAndTheDialogStaysOpen()
    {
        steam.AppData = Result.Fail("This AppID doesn't exist on Steam");
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;
        viewModel.AppIdText = "1";

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal("This AppID doesn't exist on Steam", viewModel.ErrorMessage);
        Assert.False(closeRequested);
        Assert.Null(viewModel.Result);
    }

    [Fact]
    public async Task Search_FillsResultsAndSelectingOneFillsTheAppId()
    {
        steam.SearchResults = Result.Ok(new List<SteamSearchResult> { new(620, "Portal 2"), new(400, "Portal") });
        viewModel.SearchText = "portal";

        await viewModel.SearchCommand.ExecuteAsync(null);
        viewModel.SelectedSearchResult = viewModel.SearchResults[1];

        Assert.Equal(2, viewModel.SearchResults.Count);
        Assert.Equal("400", viewModel.AppIdText);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Search_NoResults_SaysSo()
    {
        viewModel.SearchText = "nothing";

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal("No results found", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Search_EmptyText_AsksForAName()
    {
        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal("Enter an app name to search", viewModel.ErrorMessage);
    }
}
