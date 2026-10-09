using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.Debugging.Interfaces;
using InternalsViewer.Query.XEvents;
using InternalsViewer.UI.App.Services;

namespace InternalsViewer.UI.App.ViewModels;

public partial class SettingsViewModel(SettingsService settingsService, TraceDirectoryService traceDirectoryService)
    : ObservableObject, IWinDbgSettings
{
    public const char CategorySeparator = ';';

    private const string SymbolsPathKey = "SymbolsPath";
    private const string DefaultSymbolsPath = @"C:\Symbols";

    private const string UseCustomTraceDirectoryKey = "UseCustomTraceDirectory";
    private const string TraceDirectoryKey = "TraceDirectory";
    private const string MaxTraceSizeKey = "MaxTraceSizeMb";
    private const string AutoDeleteTraceKey = "AutoDeleteTrace";
    private const string TraceNestedLayoutKey = "TraceNestedLayout";
    private const string PlanAnnotationsKey = "PlanAnnotations";

    private const string ColumnstoreResolutionKey = "FullColumnstoreAllocationResolution";

    private const string WinDbgPasswordKey = "WinDbgPassword";
    private const string WinDbgPathKey = "WinDbgPath";
    private const string SymbolSearchExcludedModulesKey = "SymbolSearchExcludedModules";
    private const string ShowTimeTravelWarningKey = "ShowTimeTravelWarning";
    private const string CallTreeHiddenCategoriesKey = "CallTreeHiddenCategories";

    private const string DefaultCallTreeHiddenCategories =
        "Compilation;Execution Tree;Expression Evaluation;Metadata;Networking;Query Binding;Query Store;Security";

    private const double DefaultMaxTraceSizeMb = 150;

    [ObservableProperty]
    private string _symbolsPath = DefaultSymbolsPath;

    [ObservableProperty]
    private bool _planAnnotations;

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

    [ObservableProperty]
    private bool _fullColumnstoreResolution = true;

    [ObservableProperty]
    private string _winDbgPassword = string.Empty;

    [ObservableProperty]
    private string _winDbgPath = string.Empty;

    [ObservableProperty]
    private string _symbolSearchExcludedModules = string.Empty;

    [ObservableProperty]
    private bool _showTimeTravelWarning = true;

    [ObservableProperty]
    private string _callTreeHiddenCategories = DefaultCallTreeHiddenCategories;

    [ObservableProperty]
    private string _memoryUsage = string.Empty;

    public string? ActiveTraceDirectory =>
        UseCustomTraceDirectory && !string.IsNullOrWhiteSpace(TraceDirectory) ? TraceDirectory : null;

    private SettingsService SettingsService { get; } = settingsService;

    private TraceDirectoryService TraceDirectoryService { get; } = traceDirectoryService;

    public static HashSet<string> SplitCategories(string value)
        => new(value.Split(CategorySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
               StringComparer.Ordinal);

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

        var savedColumnstoreResolution = await SettingsService.ReadSettingAsync<bool?>(ColumnstoreResolutionKey);

        FullColumnstoreResolution = savedColumnstoreResolution ?? true;

        var savedPlanAnnotations = await SettingsService.ReadSettingAsync<bool?>(PlanAnnotationsKey);

        PlanAnnotations = savedPlanAnnotations ?? false;

        var savedWinDbgPassword = await SettingsService.ReadSettingAsync<string>(WinDbgPasswordKey);

        WinDbgPassword = string.IsNullOrWhiteSpace(savedWinDbgPassword) ? Guid.NewGuid().ToString("N")[..12] : savedWinDbgPassword;

        WinDbgPath = await SettingsService.ReadSettingAsync<string>(WinDbgPathKey) ?? string.Empty;

        SymbolSearchExcludedModules = await SettingsService.ReadSettingAsync<string>(SymbolSearchExcludedModulesKey) ?? string.Empty;

        ShowTimeTravelWarning = await SettingsService.ReadSettingAsync<bool?>(ShowTimeTravelWarningKey) ?? true;

        CallTreeHiddenCategories = await SettingsService.ReadSettingAsync<string>(CallTreeHiddenCategoriesKey)
                                   ?? DefaultCallTreeHiddenCategories;
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

    partial void OnWinDbgPasswordChanged(string value)
    {
        _ = SettingsService.SaveSettingAsync(WinDbgPasswordKey, value);
    }

    partial void OnWinDbgPathChanged(string value)
    {
        _ = SettingsService.SaveSettingAsync(WinDbgPathKey, value);
    }

    partial void OnSymbolSearchExcludedModulesChanged(string value)
    {
        _ = SettingsService.SaveSettingAsync(SymbolSearchExcludedModulesKey, value);
    }

    partial void OnShowTimeTravelWarningChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(ShowTimeTravelWarningKey, value);
    }

    partial void OnCallTreeHiddenCategoriesChanged(string value)
    {
        _ = SettingsService.SaveSettingAsync(CallTreeHiddenCategoriesKey, value);
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

    partial void OnFullColumnstoreResolutionChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(ColumnstoreResolutionKey, value);
    }

    partial void OnTraceNestedLayoutChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceNestedLayoutKey, value);
    }

    partial void OnPlanAnnotationsChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(PlanAnnotationsKey, value);
    }
}
