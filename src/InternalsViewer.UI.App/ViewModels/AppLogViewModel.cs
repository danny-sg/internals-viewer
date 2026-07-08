using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Services.Logging;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;

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

    [RelayCommand]
    private async Task ExportLog()
    {
        var picker = new FileSavePicker();

        WinRT.Interop.InitializeWithWindow.Initialize(
            picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = $"internals-viewer-log-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        picker.FileTypeChoices.Add("Text file", [".txt"]);

        var file = await picker.PickSaveFileAsync();

        if (file == null)
        {
            return;
        }

        var lines = LogEntries.Select(FormatLogEntry);

        await File.WriteAllLinesAsync(file.Path, lines);
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var line = $"{entry.TimestampText} [{entry.Level,-12}] {entry.ShortCategory}: {entry.Message}";

        if (entry.Exception != null)
        {
            line += Environment.NewLine + entry.Exception;
        }

        return line;
    }

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