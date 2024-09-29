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

        Mouse.OverrideCursor = Cursors.Wait;

        if (string.IsNullOrEmpty(text) || !int.TryParse(text, out int appId))
        {
            ErrorMessage = "The provided AppID is not valid";
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
