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

namespace InternalsViewer.UI.App.ViewModels.Connections;

public static class ConnectBackupViewModelFactory
{
    public static ConnectBackupViewModel Create() => new();
}

public partial class ConnectBackupViewModel : ObservableObject
{
    private string _lastMessage = string.Empty;

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

        _lastMessage = string.Empty;

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
        if (detail.Message != _lastMessage)
        {
            _lastMessage = detail.Message;

            ProgressLog = ProgressLog.Length == 0
                          ? detail.Message
                          : $"{ProgressLog}{Environment.NewLine}{detail.Message}";
        }

        IsProgressIndeterminate = detail.IsIndeterminate;

        ProgressPercentage = detail.Percentage ?? 0;
    }

    private void Validate()
    {
        IsValid = Filenames.Count > 0 && Filenames.All(File.Exists);
    }
}