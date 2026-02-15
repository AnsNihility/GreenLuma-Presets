using GreenLumaPresets.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace GreenLumaPresets.Views;

public partial class ImportFromSteamWindow : Window, INotifyPropertyChanged
{
    private readonly SteamService steamDbService;
    private string? errorMessage;
    private const string AppIdPlaceholder = "e.g., 730";
    private const string AppNamePlaceholder = "e.g., Counter-Strike 2";

    public ImportFromSteamWindow(Window owner)
    {
        this.steamDbService = App.Current.Services.GetService<SteamService>()
            ?? throw new ArgumentException(nameof(steamDbService));

        this.Owner = owner;
        InitializeComponent();
        DataContext = this;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        string text = AppIdTextBox.Text;

        // Clear placeholder if it still shows
        if (text == AppIdPlaceholder)
        {
            text = "";
        }

        Mouse.OverrideCursor = Cursors.Wait;

        if (string.IsNullOrEmpty(text) || !int.TryParse(text, out int appId))
        {
            ErrorMessage = "The provided AppID is not valid";
            Mouse.OverrideCursor = null;
            return;
        }

        var appDataResult = await steamDbService.GetAppData(appId);
        Mouse.OverrideCursor = null;

        if (appDataResult.IsSuccess)
        {
            AppName = appDataResult.Value.Name;
            AppIds = new List<string>(appDataResult.Value.AppIds);
            Close();
            return;
        }

        ErrorMessage = appDataResult.Errors.FirstOrDefault()?.Message;
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        string text = AppNameTextBox.Text;

        if (text == AppNamePlaceholder)
        {
            text = "";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            ErrorMessage = "Enter an app name to search";
            return;
        }

        ErrorMessage = null;
        SearchResults.Clear();
        Mouse.OverrideCursor = Cursors.Wait;

        var searchResult = await steamDbService.SearchAppsByName(text);
        Mouse.OverrideCursor = null;

        if (!searchResult.IsSuccess)
        {
            ErrorMessage = searchResult.Errors.FirstOrDefault()?.Message;
            return;
        }

        foreach (var item in searchResult.Value)
        {
            SearchResults.Add(item);
        }

        if (SearchResults.Count == 0)
        {
            ErrorMessage = "No results found";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return; // No double-click action for non-resizable window
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TitleBar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        SystemCommands.ShowSystemMenu(this, screenPoint);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AppIdTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (AppIdTextBox.Text == AppIdPlaceholder)
        {
            AppIdTextBox.Text = "";
            AppIdTextBox.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 242, 213)); // SteamText color
        }
    }

    private void AppIdTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AppIdTextBox.Text))
        {
            AppIdTextBox.Text = AppIdPlaceholder;
            AppIdTextBox.Foreground = (System.Windows.Media.Brush)App.Current.Resources["SteamMutedText"];
        }
    }

    private void AppNameTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (AppNameTextBox.Text == AppNamePlaceholder)
        {
            AppNameTextBox.Text = "";
            AppNameTextBox.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 242, 213)); // SteamText color
        }
    }

    private void AppNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AppNameTextBox.Text))
        {
            AppNameTextBox.Text = AppNamePlaceholder;
            AppNameTextBox.Foreground = (System.Windows.Media.Brush)App.Current.Resources["SteamMutedText"];
        }
    }

    private void SearchResultsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is SteamSearchResult selected)
        {
            SetAppIdText(selected.AppId.ToString());
        }
    }

    private void SetAppIdText(string appId)
    {
        AppIdTextBox.Text = appId;
        AppIdTextBox.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 242, 213)); // SteamText color
    }

    public List<string> AppIds { get; set; } = [];
    public string AppName { get; set; } = string.Empty;
    public ObservableCollection<SteamSearchResult> SearchResults { get; } = [];

    public string? ErrorMessage
    {
        get => errorMessage;
        set
        {
            errorMessage = value;
            OnPropertyChanged(nameof(ErrorMessage));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
