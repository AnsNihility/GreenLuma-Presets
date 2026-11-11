using GreenLumaPresets.ViewModels;
using GreenLumaPresets.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace GreenLumaPresets.Services;

public class ImportFromSteamDialog(IServiceProvider services) : IImportFromSteamDialog
{
    public SteamApp? Show()
    {
        var viewModel = services.GetRequiredService<ImportFromSteamViewModel>();
        var window = new ImportFromSteamWindow(viewModel)
        {
            Owner = WindowOwner.Find(),
        };
        window.ShowDialog();
        return viewModel.Result;
    }
}
