using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreenLumaPresets.Services;
using System.Collections.ObjectModel;

namespace GreenLumaPresets.ViewModels;

/// <summary>
/// Picks a Steam app, by AppID or by searching its name, and fetches it with its DLC.
/// </summary>
public partial class ImportFromSteamViewModel(ISteamService steamService) : ObservableObject
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string appIdText = string.Empty;

    [ObservableProperty]
    private SteamSearchResult? selectedSearchResult;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<SteamSearchResult> SearchResults { get; } = [];

    /// <summary>
    /// The app and its DLC once confirmed; null when the dialog was cancelled.
    /// </summary>
    public SteamApp? Result { get; private set; }

    public event EventHandler? CloseRequested;

    partial void OnSelectedSearchResultChanged(SteamSearchResult? value)
    {
        if (value != null) AppIdText = value.AppId.ToString();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ErrorMessage = "Enter an app name to search";
            return;
        }

        ErrorMessage = null;
        SearchResults.Clear();

        IsBusy = true;
        try
        {
            var searchResult = await steamService.SearchAppsByName(SearchText.Trim());
            if (searchResult.IsFailed)
            {
                ErrorMessage = searchResult.Errors.FirstOrDefault()?.Message;
                return;
            }

            foreach (var item in searchResult.Value)
            {
                SearchResults.Add(item);
            }

            if (SearchResults.Count == 0) ErrorMessage = "No results found";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (!int.TryParse(AppIdText.Trim(), out var appId) || appId <= 0)
        {
            ErrorMessage = "The provided AppID is not valid";
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var appDataResult = await steamService.GetAppData(appId);
            if (appDataResult.IsFailed)
            {
                ErrorMessage = appDataResult.Errors.FirstOrDefault()?.Message;
                return;
            }

            Result = appDataResult.Value;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
