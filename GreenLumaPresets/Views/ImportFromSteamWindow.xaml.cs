using GreenLumaPresets.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace GreenLumaPresets.Views;

public partial class ImportFromSteamWindow : Window, INotifyPropertyChanged
{
    private readonly SteamService steamDbService;
    private string? errorMessage;

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
        if (text == "e.g., 730")
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
        if (AppIdTextBox.Text == "e.g., 730")
        {
            AppIdTextBox.Text = "";
            AppIdTextBox.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 242, 213)); // SteamText color
        }
    }

    private void AppIdTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(AppIdTextBox.Text))
        {
            AppIdTextBox.Text = "e.g., 730";
            AppIdTextBox.Foreground = (System.Windows.Media.Brush)App.Current.Resources["SteamMutedText"];
        }
    }

    public List<string> AppIds { get; set; } = [];
    public string AppName { get; set; } = string.Empty;

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
