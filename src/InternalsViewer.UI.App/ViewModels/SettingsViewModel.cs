using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.Services.XEvents;

namespace InternalsViewer.UI.App.ViewModels;

public partial class SettingsViewModel(SettingsService settingsService, TraceDirectoryService traceDirectoryService)
    : ObservableObject
{
    private const string SymbolsPathKey = "SymbolsPath";
    private const string DefaultSymbolsPath = @"C:\Symbols";

    private const string UseCustomTraceDirectoryKey = "UseCustomTraceDirectory";
    private const string TraceDirectoryKey = "TraceDirectory";
    private const string MaxTraceSizeKey = "MaxTraceSizeMb";
    private const string AutoDeleteTraceKey = "AutoDeleteTrace";
    private const string TraceNestedLayoutKey = "TraceNestedLayout";

    private const double DefaultMaxTraceSizeMb = 150;

    private SettingsService SettingsService { get; } = settingsService;

    private TraceDirectoryService TraceDirectoryService { get; } = traceDirectoryService;

    [ObservableProperty]
    private string _symbolsPath = DefaultSymbolsPath;

    [ObservableProperty]
    private bool _useCustomTraceDirectory;

    [ObservableProperty]
    private string _traceDirectory = string.Empty;

    [ObservableProperty]
    private string _traceDirectoryStatus = string.Empty;

    [ObservableProperty]
    private double _maxTraceSizeMb = DefaultMaxTraceSizeMb;

    [ObservableProperty]
    private bool _autoDeleteTrace = true;

    [ObservableProperty]
    private bool _traceNestedLayout = true;

    public string? ActiveTraceDirectory =>
        UseCustomTraceDirectory && !string.IsNullOrWhiteSpace(TraceDirectory) ? TraceDirectory : null;

    [ObservableProperty]
    private string _memoryUsage = string.Empty;

    public void RefreshMemoryUsage()
    {
        var bytes = GC.GetTotalMemory(false);

        MemoryUsage = bytes >= 1024 * 1024 * 1024
                      ? $"{bytes / 1024d / 1024d / 1024d:N2} GB"
                      : $"{bytes / 1024d / 1024d:N0} MB";
    }

    public async Task LoadAsync()
    {
        var savedSymbols = await SettingsService.ReadSettingAsync<string>(SymbolsPathKey);

        SymbolsPath = string.IsNullOrWhiteSpace(savedSymbols) ? DefaultSymbolsPath : savedSymbols;

        var savedDirectory = await SettingsService.ReadSettingAsync<string>(TraceDirectoryKey);

        TraceDirectory = string.IsNullOrWhiteSpace(savedDirectory)
            ? TraceDirectoryService.DefaultDirectory
            : savedDirectory;

        UseCustomTraceDirectory = await SettingsService.ReadSettingAsync<bool>(UseCustomTraceDirectoryKey);

        var savedSize = await SettingsService.ReadSettingAsync<double?>(MaxTraceSizeKey);

        MaxTraceSizeMb = savedSize is > 0 ? savedSize.Value : DefaultMaxTraceSizeMb;

        var savedAutoDelete = await SettingsService.ReadSettingAsync<bool?>(AutoDeleteTraceKey);

        AutoDeleteTrace = savedAutoDelete ?? true;

        var savedNestedLayout = await SettingsService.ReadSettingAsync<bool?>(TraceNestedLayoutKey);

        TraceNestedLayout = savedNestedLayout ?? true;
    }

    /// <summary>
    /// Grants the local SQL Server service accounts write access to the custom trace directory
    /// </summary>
    [RelayCommand]
    private void GrantPermissions()
    {
        var result = TraceDirectoryService.GrantPermissions(TraceDirectory);

        TraceDirectoryStatus = result.Message;
    }

    partial void OnSymbolsPathChanged(string value)
    {
        _ = SettingsService.SaveSettingAsync(SymbolsPathKey, value);
    }

    partial void OnUseCustomTraceDirectoryChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(UseCustomTraceDirectoryKey, value);
    }

    partial void OnTraceDirectoryChanged(string value)
    {
        _ = SettingsService.SaveSettingAsync(TraceDirectoryKey, value);
    }

    partial void OnMaxTraceSizeMbChanged(double value)
    {
        _ = SettingsService.SaveSettingAsync(MaxTraceSizeKey, value);
    }

    partial void OnAutoDeleteTraceChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(AutoDeleteTraceKey, value);
    }

    partial void OnTraceNestedLayoutChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceNestedLayoutKey, value);
    }
}
