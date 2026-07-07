using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Services;

namespace InternalsViewer.UI.App.ViewModels;

public partial class SettingsViewModel(SettingsService settingsService) : ObservableObject
{
    private const string SymbolsPathKey = "SymbolsPath";
    private const string DefaultSymbolsPath = @"C:\Symbols";

    private SettingsService SettingsService { get; } = settingsService;

    [ObservableProperty]
    private string _symbolsPath = DefaultSymbolsPath;

    public async Task LoadAsync()
    {
        var saved = await SettingsService.ReadSettingAsync<string>(SymbolsPathKey);

        SymbolsPath = string.IsNullOrWhiteSpace(saved) ? DefaultSymbolsPath : saved;
    }

    partial void OnSymbolsPathChanged(string value)
    {
        _ = SettingsService.SaveSettingAsync(SymbolsPathKey, value);
    }
}
