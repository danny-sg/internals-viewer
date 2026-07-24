using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Connections;

namespace InternalsViewer.UI.App.ViewModels.Connections;

public static class ConnectBackupViewModelFactory
{
    public static ConnectBackupViewModel Create() => new();
}

public partial class ConnectBackupViewModel : ObservableObject
{
    public ObservableCollection<string> Filenames { get; } = [];

    [ObservableProperty]
    private bool isValid;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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
    private async Task Connect()
    {
        ErrorMessage = string.Empty;

        IsBusy = true;

        try
        {
            var recent = new RecentConnection
            {
                Name = Path.GetFileName(Filenames[0]),
                ConnectionType = "Backup",
                Value = string.Join(";", Filenames)
            };

            var message = new ConnectBackupMessage(recent.Value, recent);

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
        }
    }

    private void Validate()
    {
        IsValid = Filenames.Count > 0 && Filenames.All(File.Exists);
    }
}