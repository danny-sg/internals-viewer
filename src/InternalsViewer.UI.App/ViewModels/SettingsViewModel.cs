using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;
using InternalsViewer.Query.FullTrace.Recording;
using InternalsViewer.Query.XEvents;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.Mcp;

namespace InternalsViewer.UI.App.ViewModels;

public partial class SettingsViewModel(SettingsService settingsService,
                                       TraceDirectoryService traceDirectoryService,
                                       McpServerHost mcpServerHost,
                                       IEnumerable<IMcpClient> mcpClients)
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
    private const string IsTracePromptEnabledKey = "IsTracePromptEnabled";
    private const string CallTreeHiddenCategoriesKey = "CallTreeHiddenCategories";
    private const string CallTreePrunedCategoriesKey = "CallTreePrunedCategories";
    private const string TraceTimelineShowMemoryKey = "FlameChartShowMemory";
    private const string TraceTimelineMemoryModeKey = "FlameChartMemoryMode";
    private const string TraceTimelineMemorySizeToFitKey = "FlameChartMemorySizeToFit";
    private const string TraceTimelineShowMemoryWritesKey = "TraceTimelineShowMemoryWrites";
    private const string TraceTimelineShowMemoryReadsKey = "TraceTimelineShowMemoryReads";
    private const string TraceTimelineShowBufferLatchesKey = "TraceTimelineShowBufferLatches";
    private const string TraceTimelineShowBuffersKey = "TraceTimelineShowBuffers";
    private const string TraceTimelineShowOperatorsKey = "TraceTimelineShowOperators";
    private const string TraceTimelineShowMemoryHistoryKey = "TraceTimelineShowMemoryHistory";
    private const string TraceTimelineShowCallsKey = "TraceTimelineShowCalls";
    private const string PinTimelineKey = "PinTimeline";

    private const string TraceTimelineHiddenTickTracksKey = "TraceTimelineHiddenTickTracks";
    private const string PlanAdditionalStepsKey = "PlanAdditionalSteps";

    private const string PlanDetailedStepsKey = "PlanDetailedSteps";
    private const string DecodeValuesKey = "DecodeValues";
    private const string McpServerEnabledKey = "McpServerEnabled";
    private const string McpServerPortKey = "McpServerPort";
    private const string McpServerTokenKey = "McpServerToken";

    private const char HiddenTokenCharacter = '\u25CF';

    private const string McpServerStoppedStatus = "Stopped";

    private const string DefaultCallTreeHiddenCategories =
        "Compilation;Networking;Query Store;Security;Tracing;Transaction Management";

    private const string DefaultTraceTimelineHiddenTickTracks = "Open";

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
    private bool _isTracePromptEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CallTreeHiddenCategorySet))]
    private string _callTreeHiddenCategories = DefaultCallTreeHiddenCategories;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CallTreePrunedCategorySet))]
    private string _callTreePrunedCategories = string.Empty;

    [ObservableProperty]
    private bool _traceTimelineShowMemory;

    [ObservableProperty]
    private bool _traceTimelineShowOperators = true;

    [ObservableProperty]
    private bool _traceTimelineShowMemoryHistory = true;

    [ObservableProperty]
    private bool _traceTimelineShowCalls = true;

    [ObservableProperty]
    private TraceTimelineMemoryMode _traceTimelineMemoryMode;

    [ObservableProperty]
    private bool _traceTimelineMemorySizeToFit = true;

    [ObservableProperty]
    private bool _traceTimelineShowMemoryWrites = true;

    [ObservableProperty]
    private bool _traceTimelineShowMemoryReads;

    [ObservableProperty]
    private bool _traceTimelineShowBufferLatches = true;

    [ObservableProperty]
    private bool _traceTimelineShowBuffers;

    [ObservableProperty]
    private bool _pinTimeline = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TraceTimelineHiddenTickTrackSet))]
    private string _traceTimelineHiddenTickTracks = DefaultTraceTimelineHiddenTickTracks;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanAdditionalStepSet))]
    private string _planAdditionalSteps = string.Empty;

    [ObservableProperty]
    private bool _planDetailedSteps;

    [ObservableProperty]
    private bool _decodeValues = true;

    [ObservableProperty]
    private string _memoryUsage = string.Empty;

    [ObservableProperty]
    private bool _mcpServerEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(McpServerEndpoint))]
    private double _mcpServerPort = McpHostSettings.DefaultPort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(McpServerTokenText))]
    private string _mcpServerToken = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(McpServerTokenText))]
    private bool _isMcpServerTokenVisible;

    [ObservableProperty]
    private string _mcpServerStatus = McpServerStoppedStatus;

    public string McpServerEndpoint => GetMcpHostSettings().Endpoint;

    public string McpServerTokenText => IsMcpServerTokenVisible ? McpServerToken : new string(HiddenTokenCharacter, McpServerToken.Length);

    public ObservableCollection<McpClientViewModel> McpClients
    {
        get => field ??= new ObservableCollection<McpClientViewModel>(
            McpClientSources.Select(c => new McpClientViewModel(c, GetMcpHostSettings)));
    }

    public string? ActiveTraceDirectory =>
        UseCustomTraceDirectory && !string.IsNullOrWhiteSpace(TraceDirectory) ? TraceDirectory : null;

    public IReadOnlySet<string> CallTreeHiddenCategorySet { get; private set; } = SplitCategories(DefaultCallTreeHiddenCategories);

    public IReadOnlySet<string> CallTreePrunedCategorySet { get; private set; } = SplitCategories(string.Empty);

    public IReadOnlySet<string> TraceTimelineHiddenTickTrackSet { get; private set; }
        = SplitCategories(DefaultTraceTimelineHiddenTickTracks);

    public IReadOnlySet<string> PlanAdditionalStepSet { get; private set; } = SplitCategories(string.Empty);

    private SettingsService SettingsService { get; } = settingsService;

    private TraceDirectoryService TraceDirectoryService { get; } = traceDirectoryService;

    private McpServerHost McpServerHost { get; } = mcpServerHost;

    private IEnumerable<IMcpClient> McpClientSources { get; } = mcpClients;

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

        IsTracePromptEnabled = await SettingsService.ReadSettingAsync<bool?>(IsTracePromptEnabledKey) ?? true;

        CallTreeHiddenCategories = await SettingsService.ReadSettingAsync<string>(CallTreeHiddenCategoriesKey)
                                   ?? DefaultCallTreeHiddenCategories;

        CallTreePrunedCategories = await SettingsService.ReadSettingAsync<string>(CallTreePrunedCategoriesKey) ?? string.Empty;

        TraceTimelineShowMemory = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowMemoryKey) ?? false;

        TraceTimelineShowOperators = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowOperatorsKey) ?? true;

        TraceTimelineShowMemoryHistory = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowMemoryHistoryKey) ?? true;

        TraceTimelineShowCalls = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowCallsKey) ?? true;

        TraceTimelineMemoryMode = await SettingsService.ReadSettingAsync<TraceTimelineMemoryMode?>(TraceTimelineMemoryModeKey)
                               ?? TraceTimelineMemoryMode.Allocated;

        TraceTimelineMemorySizeToFit = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineMemorySizeToFitKey) ?? true;

        TraceTimelineShowMemoryWrites = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowMemoryWritesKey) ?? true;

        TraceTimelineShowMemoryReads = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowMemoryReadsKey) ?? false;

        TraceTimelineShowBufferLatches = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowBufferLatchesKey) ?? true;

        TraceTimelineShowBuffers = await SettingsService.ReadSettingAsync<bool?>(TraceTimelineShowBuffersKey) ?? false;

        PinTimeline = await SettingsService.ReadSettingAsync<bool?>(PinTimelineKey) ?? true;

        TraceTimelineHiddenTickTracks = await SettingsService.ReadSettingAsync<string>(TraceTimelineHiddenTickTracksKey)
                                        ?? DefaultTraceTimelineHiddenTickTracks;

        PlanAdditionalSteps = await SettingsService.ReadSettingAsync<string>(PlanAdditionalStepsKey) ?? string.Empty;

        PlanDetailedSteps = await SettingsService.ReadSettingAsync<bool?>(PlanDetailedStepsKey) ?? false;

        DecodeValues = await SettingsService.ReadSettingAsync<bool?>(DecodeValuesKey) ?? true;

        var savedPort = await SettingsService.ReadSettingAsync<double?>(McpServerPortKey);

        McpServerPort = savedPort is >= 1 and <= 65535 ? savedPort.Value : McpHostSettings.DefaultPort;

        var savedToken = PasswordProtection.Unprotect(await SettingsService.ReadSettingAsync<string>(McpServerTokenKey));

        if (string.IsNullOrEmpty(savedToken))
        {
            await SaveMcpTokenAsync(McpServerHost.CreateToken());
        }
        else
        {
            McpServerToken = savedToken;
        }

        McpServerEnabled = await SettingsService.ReadSettingAsync<bool?>(McpServerEnabledKey) ?? false;

        await RefreshMcpClientsAsync();
    }

    [RelayCommand]
    private async Task RegenerateMcpTokenAsync()
    {
        await SaveMcpTokenAsync(McpServerHost.CreateToken());

        if (McpServerHost.IsRunning)
        {
            await StartMcpServerAsync();
        }

        await UpdateRegisteredMcpClientsAsync();
    }

    [RelayCommand]
    private void CopyMcpServerEndpoint() => ClipboardText.Copy(McpServerEndpoint);

    [RelayCommand]
    private void CopyMcpServerToken() => ClipboardText.Copy(McpServerToken);

    private McpHostSettings GetMcpHostSettings() => new((int)McpServerPort, McpServerToken);

    private async Task RefreshMcpClientsAsync()
    {
        foreach (var client in McpClients)
        {
            await client.RefreshAsync();
        }
    }

    private async Task UpdateRegisteredMcpClientsAsync()
    {
        foreach (var client in McpClients)
        {
            await client.UpdateIfRegisteredAsync();
        }
    }

    private async Task SaveMcpTokenAsync(string token)
    {
        McpServerToken = token;

        await SettingsService.SaveSettingAsync(McpServerTokenKey, PasswordProtection.Protect(token));
    }

    private async Task StartMcpServerAsync()
    {
        try
        {
            McpServerStatus = "Starting";

            await McpServerHost.StartAsync(new McpHostSettings((int)McpServerPort, McpServerToken), CancellationToken.None);

            McpServerStatus = $"Running on {McpServerEndpoint}";
        }
        catch (Exception exception)
        {
            McpServerStatus = $"Failed to start: {exception.Message}";
        }
    }

    private async Task StopMcpServerAsync()
    {
        await McpServerHost.StopAsync();

        McpServerStatus = McpServerStoppedStatus;
    }

    partial void OnMcpServerEnabledChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(McpServerEnabledKey, value);

        _ = value ? StartMcpServerAsync() : StopMcpServerAsync();
    }

    partial void OnMcpServerPortChanged(double value)
    {
        _ = SettingsService.SaveSettingAsync(McpServerPortKey, value);

        if (McpServerHost.IsRunning)
        {
            _ = StartMcpServerAsync();
        }

        if (McpServerToken.Length > 0)
        {
            _ = UpdateRegisteredMcpClientsAsync();
        }
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

    partial void OnIsTracePromptEnabledChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(IsTracePromptEnabledKey, value);
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

    partial void OnPlanAdditionalStepsChanged(string value)
    {
        PlanAdditionalStepSet = SplitCategories(value);

        _ = SettingsService.SaveSettingAsync(PlanAdditionalStepsKey, value);
    }

    partial void OnPlanDetailedStepsChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(PlanDetailedStepsKey, value);
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

    partial void OnTraceTimelineMemoryModeChanged(TraceTimelineMemoryMode value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineMemoryModeKey, value);
    }

    partial void OnTraceTimelineMemorySizeToFitChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineMemorySizeToFitKey, value);
    }

    partial void OnTraceTimelineShowOperatorsChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowOperatorsKey, value);
    }

    partial void OnTraceTimelineShowMemoryHistoryChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowMemoryHistoryKey, value);
    }

    partial void OnTraceTimelineShowCallsChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowCallsKey, value);
    }

    partial void OnTraceTimelineShowMemoryWritesChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowMemoryWritesKey, value);
    }

    partial void OnTraceTimelineShowMemoryReadsChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowMemoryReadsKey, value);
    }

    partial void OnTraceTimelineShowBufferLatchesChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowBufferLatchesKey, value);
    }

    partial void OnTraceTimelineShowBuffersChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TraceTimelineShowBuffersKey, value);
    }

    partial void OnPinTimelineChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(PinTimelineKey, value);
    }

    partial void OnDecodeValuesChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(DecodeValuesKey, value);
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
