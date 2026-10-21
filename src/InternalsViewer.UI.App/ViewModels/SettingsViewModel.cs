using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.XEvents;
using InternalsViewer.UI.App.Controls.Trace;
using InternalsViewer.UI.App.Services;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Recording;

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
    private const string SimulationNestedLayoutKey = "TraceNestedLayout";
    private const string PlanAnnotationsKey = "PlanAnnotations";

    private const string ColumnstoreResolutionKey = "FullColumnstoreAllocationResolution";

    private const string WinDbgPasswordKey = "WinDbgPassword";
    private const string WinDbgPathKey = "WinDbgPath";
    private const string SymbolSearchExcludedModulesKey = "SymbolSearchExcludedModules";
    private const string ShowTimeTravelWarningKey = "ShowTimeTravelWarning";
    private const string CallTreeHiddenCategoriesKey = "CallTreeHiddenCategories";
    private const string CallTreePrunedCategoriesKey = "CallTreePrunedCategories";
    private const string TraceTimelineShowMemoryKey = "FlameChartShowMemory";
    private const string TraceTimelineShowCallStackKey = "FlameChartShowCallStack";
    private const string TraceTimelineMemoryModeKey = "FlameChartMemoryMode";

    private const string TraceTimelineHiddenTickTracksKey = "TraceTimelineHiddenTickTracks";
    private const string DecodeValuesKey = "DecodeValues";
    private const string CustomHooksKey = "CustomHooks";

    private const string DefaultCallTreeHiddenCategories =
        "Compilation;Metadata;Networking;Query Store;Security;Tracing;Transaction Management";

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
    private bool _simulationNestedLayout = true;

    [ObservableProperty]
    private bool _fullColumnstoreResolution = true;

    [ObservableProperty]
    private string _winDbgPath = string.Empty;

    [ObservableProperty]
    private string _symbolSearchExcludedModules = string.Empty;

    [ObservableProperty]
    private bool _showTimeTravelWarning = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CallTreeHiddenCategorySet))]
    private string _callTreeHiddenCategories = DefaultCallTreeHiddenCategories;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CallTreePrunedCategorySet))]
    private string _callTreePrunedCategories = string.Empty;

    [ObservableProperty]
    private bool _traceTimelineShowMemory;

    [ObservableProperty]
    private bool _traceTimelineShowCallStack;

    [ObservableProperty]
    private TraceTimelineMemoryMode _traceTimelineMemoryMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TraceTimelineHiddenTickTrackSet))]
    private string _traceTimelineHiddenTickTracks = string.Empty;

    [ObservableProperty]
    private bool _decodeValues = true;

    [ObservableProperty]
    private IReadOnlyList<CustomHookDefinition> _customHooks = [];

    [ObservableProperty]
    private string _memoryUsage = string.Empty;

    public string? ActiveTraceDirectory =>
        UseCustomTraceDirectory && !string.IsNullOrWhiteSpace(TraceDirectory) ? TraceDirectory : null;

    public IReadOnlySet<string> CallTreeHiddenCategorySet { get; private set; } = SplitCategories(DefaultCallTreeHiddenCategories);

    public IReadOnlySet<string> CallTreePrunedCategorySet { get; private set; } = SplitCategories(string.Empty);

    public IReadOnlySet<string> TraceTimelineHiddenTickTrackSet { get; private set; } = SplitCategories(string.Empty);

    private SettingsService SettingsService { get; } = settingsService;

    private TraceDirectoryService TraceDirectoryService { get; } = traceDirectoryService;

    public void AddCustomHook(CustomHookDefinition definition)
        => CustomHooks = [.. CustomHooks.Where(h => h.Name != definition.Name), definition];

    public void RemoveCustomHook(CustomHookDefinition definition) => CustomHooks = [.. CustomHooks.Where(h => h != definition)];

    public void SetCustomHookPaused(CustomHookDefinition definition, bool isPaused)
        => CustomHooks = [.. CustomHooks.Select(h => h == definition ? h with { IsPaused = isPaused } : h)];

    public void ResetCallTreeCategories()
    {
        CallTreeHiddenCategories = DefaultCallTreeHiddenCategories;

        CallTreePrunedCategories = string.Empty;
    }

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

        var savedNestedLayout = await SettingsService.ReadSettingAsync<bool?>(SimulationNestedLayoutKey);

        SimulationNestedLayout = savedNestedLayout ?? true;

        var savedColumnstoreResolution = await SettingsService.ReadSettingAsync<bool?>(ColumnstoreResolutionKey);

        FullColumnstoreResolution = savedColumnstoreResolution ?? true;

        var savedPlanAnnotations = await SettingsService.ReadSettingAsync<bool?>(PlanAnnotationsKey);

        PlanAnnotations = savedPlanAnnotations ?? false;

        await SettingsService.RemoveSettingAsync(WinDbgPasswordKey);

        WinDbgPath = await SettingsService.ReadSettingAsync<string>(WinDbgPathKey) ?? string.Empty;

        SymbolSearchExcludedModules = await SettingsService.ReadSettingAsync<string>(SymbolSearchExcludedModulesKey) ?? string.Empty;

        ShowTimeTravelWarning = await SettingsService.ReadSettingAsync<bool?>(ShowTimeTravelWarningKey) ?? true;

        CallTreeHiddenCategories = await SettingsService.ReadSettingAsync<string>(CallTreeHiddenCategoriesKey)
                                   ?? DefaultCallTreeHiddenCategories;

        CallTreePrunedCategories = await SettingsService.ReadSettingAsync<string>(CallTreePrunedCategoriesKey) ?? string.Empty;

        TraceTimelineShowMemory = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowMemoryKey) ?? false;

        TraceTimelineShowCallStack = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowCallStackKey) ?? false;

        TraceTimelineMemoryMode = await SettingsService.ReadSettingAsync<TraceTimelineMemoryMode?>(TraceTimelineMemoryModeKey)
                               ?? TraceTimelineMemoryMode.Allocated;

        TraceTimelineHiddenTickTracks = await SettingsService.ReadSettingAsync<string>(TraceTimelineHiddenTickTracksKey) ?? string.Empty;

        DecodeValues = await SettingsService.ReadSettingAsync<bool?>(DecodeValuesKey) ?? true;

        CustomHooks = await SettingsService.ReadSettingAsync<List<CustomHookDefinition>>(CustomHooksKey) ?? [];
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

    private static HashSet<string> SplitCategories(string value)
        => new(value.Split(CategorySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
               StringComparer.Ordinal);

    partial void OnCallTreeHiddenCategoriesChanged(string value)
    {
        CallTreeHiddenCategorySet = SplitCategories(value);

        _ = SettingsService.SaveSettingAsync(CallTreeHiddenCategoriesKey, value);
    }

    partial void OnCallTreePrunedCategoriesChanged(string value)
    {
        CallTreePrunedCategorySet = SplitCategories(value);

        _ = SettingsService.SaveSettingAsync(CallTreePrunedCategoriesKey, value);
    }

    partial void OnTraceTimelineHiddenTickTracksChanged(string value)
    {
        TraceTimelineHiddenTickTrackSet = SplitCategories(value);

        _ = SettingsService.SaveSettingAsync(TraceTimelineHiddenTickTracksKey, value);
    }

    partial void OnTraceTimelineShowMemoryChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowMemoryKey, value);
    }

    partial void OnTraceTimelineShowCallStackChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowCallStackKey, value);
    }

    partial void OnTraceTimelineMemoryModeChanged(TraceTimelineMemoryMode value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineMemoryModeKey, value);
    }

    partial void OnDecodeValuesChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(DecodeValuesKey, value);
    }

    partial void OnCustomHooksChanged(IReadOnlyList<CustomHookDefinition> value)
    {
        _ = SettingsService.SaveSettingAsync(CustomHooksKey, value);
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

    partial void OnSimulationNestedLayoutChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(SimulationNestedLayoutKey, value);
    }

    partial void OnPlanAnnotationsChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(PlanAnnotationsKey, value);
    }
}
