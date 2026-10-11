using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Loading;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Connections;
using InternalsViewer.UI.App.Services;

namespace InternalsViewer.UI.App.ViewModels.Connections;

public static class ConnectBackupViewModelFactory
{
    public static ConnectBackupViewModel Create() => new();
}

public partial class ConnectBackupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    private string _progressLog = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private bool _isProgressIndeterminate = true;

    public ObservableCollection<string> Filenames { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasProgress => !string.IsNullOrEmpty(ProgressLog);

    private List<string> ProgressLines { get; } = [];

    private bool IsLastLineRunning { get; set; }

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!Filenames.Contains(path))
            {
                Filenames.Add(path);
            }
        }

        ErrorMessage = string.Empty;

        Validate();
    }

    [RelayCommand]
    private void RemoveFile(string path)
    {
        Filenames.Remove(path);

        ErrorMessage = string.Empty;

        Validate();
    }

    [RelayCommand]
    private void Clear()
    {
        Filenames.Clear();

        ErrorMessage = string.Empty;

        Validate();
    }

    [RelayCommand]
    private async Task Connect()
    {
        ErrorMessage = string.Empty;

        ProgressLog = string.Empty;

        ProgressLines.Clear();

        IsLastLineRunning = false;

        TaskbarProgress.ShowIndeterminate(this);

        IsProgressIndeterminate = true;

        IsBusy = true;

        try
        {
            var recent = new RecentConnection
            {
                Name = Path.GetFileName(Filenames[0]),
                ConnectionType = "Backup",
                Value = string.Join(";", Filenames)
            };

            var message = new ConnectBackupMessage(recent.Value, recent)
            {
                Progress = new Progress<ProgressDetail>(Report)
            };

            await WeakReferenceMessenger.Default.Send(message);

            var success = await message.Response;

            if (!success)
            {
                ErrorMessage = message.ErrorMessage ?? string.Empty;
            }
        }
        finally
        {
            IsBusy = false;

            IsProgressIndeterminate = false;

            TaskbarProgress.Clear(this);
        }
    }

    /// <summary>
    /// Records a stage of the load, adding a line only when the stage itself changes
    /// </summary>
    /// <remarks>
    /// A stage reports its message unchanged while its percentage climbs, so comparing against the last message keeps a long scan to one
    /// line and lets the percentage drive the bar instead.
    ///
    /// The Progress is constructed on the UI thread, so its callback marshals back onto that thread from the load running on the thread
    /// pool.
    /// </remarks>
    private void Report(ProgressDetail detail)
    {
        var running = IsLastLineRunning && ProgressLines[^1] == detail.Message;

        if (detail.Elapsed is { } elapsed && running)
        {
            ProgressLines[^1] = $"{detail.Message} - Completed in {elapsed}";

            IsLastLineRunning = false;
        }
        else if (!running)
        {
            ProgressLines.Add(detail.Message);

            IsLastLineRunning = detail.Elapsed is null;
        }

        ProgressLog = string.Join(Environment.NewLine, ProgressLines);

        IsProgressIndeterminate = detail.Overall is null && detail.IsIndeterminate;

        ProgressPercentage = detail.Overall ?? detail.Percentage ?? 0;

        if (detail.Overall is { } overall)
        {
            TaskbarProgress.Show(this, overall);
        }
    }

    private void Validate()
    {
        IsValid = Filenames.Count > 0 && Filenames.All(File.Exists);
    }
}