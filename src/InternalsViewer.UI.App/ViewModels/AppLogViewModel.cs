using System;
using System.Collections.Generic;
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
/// Singleton ViewModel that exposes log entries from the internals layer for UI binding
/// </summary>
public partial class AppLogViewModel : TabViewModel
{
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logEntries = [];

    [ObservableProperty]
    private int _maxLogEntries = 1000;

    public AppLogViewModel(AppLogService appLogService)
    {
        AppLogService = appLogService;
        AppLogService.LogEntryReceived += OnLogEntryReceived;
    }

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                DispatcherQueue.TryEnqueue(ApplyFilter);
            }
        }
    }

    private AppLogService AppLogService { get; }

    private List<LogEntry> AllEntries { get; } = [];

    partial void OnMaxLogEntriesChanged(int value)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            while (AllEntries.Count > MaxLogEntries)
            {
                AllEntries.RemoveAt(0);
            }

            ApplyFilter();
        });
    }

    [RelayCommand]
    private void ClearLog()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AllEntries.Clear();
            LogEntries.Clear();
        });
    }

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
            while (AllEntries.Count >= MaxLogEntries)
            {
                AllEntries.RemoveAt(0);
            }

            AllEntries.Add(entry);

            ApplyFilter();
        });
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? AllEntries
            : AllEntries.Where(entry => IsMatch(entry, SearchText)).ToList();

        LogEntries.Clear();

        foreach (var entry in filtered)
        {
            LogEntries.Add(entry);
        }
    }

    private static bool IsMatch(LogEntry entry, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var term = searchText.Trim();

        if (entry.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
            || entry.ShortCategory.Contains(term, StringComparison.OrdinalIgnoreCase)
            || entry.Level.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
            || (entry.Scope?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (entry.Exception?.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }

        if (entry.Parameters is null)
        {
            return false;
        }

        foreach (var (_, value) in entry.Parameters)
        {
            if (value?.ToString()?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                return true;
            }
        }

        return false;
    }
}