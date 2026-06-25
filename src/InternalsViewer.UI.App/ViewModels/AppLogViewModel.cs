using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Services.Logging;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels;

/// <summary>
/// Singleton ViewModel that exposes log entries from the internals layer for UI binding.
/// </summary>
public partial class AppLogViewModel : TabViewModel
{
    private AppLogService AppLogService { get; }

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logEntries = [];

    public LogLevel[] LogLevels { get; } =
    [
        LogLevel.None,
        LogLevel.Critical,
        LogLevel.Error,
        LogLevel.Warning,
        LogLevel.Information,
        LogLevel.Debug,
        LogLevel.Trace,
    ];

    public LogLevel SelectedLogLevel
    {
        get => AppLogService.MinimumLevel;
        set
        {
            if (AppLogService.MinimumLevel == value)
            {
                return;
            }

            AppLogService.MinimumLevel = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    private int _maxLogEntries = 1000;

    public AppLogViewModel(AppLogService appLogService)
    {
        AppLogService = appLogService;
        AppLogService.LogEntryReceived += OnLogEntryReceived;
    }

    [RelayCommand]
    private void ClearLog() => DispatcherQueue.TryEnqueue(() => LogEntries.Clear());

    private void OnLogEntryReceived(LogEntry entry)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            while (LogEntries.Count >= MaxLogEntries)
            {
                LogEntries.RemoveAt(0);
            }

            LogEntries.Add(entry);
        });
    }
}