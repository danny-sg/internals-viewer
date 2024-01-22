using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Connections;

namespace InternalsViewer.UI.App.ViewModels.Connections;

public partial class ConnectFileViewModel : ObservableValidator
{
    [Required(AllowEmptyStrings = false)]
    [ObservableProperty]
    private string filename = string.Empty;

    [ObservableProperty]
    private bool isValid;

    partial void OnFilenameChanged(string value)
    {
        IsValid = File.Exists(value);
    }

    [RelayCommand]
    private async Task Connect()
    {
        var recent = new RecentConnection
        {
            Name = Path.GetFileNameWithoutExtension(Filename),
            ConnectionType = "File",
            Value = Filename
        };

        var message = new ConnectFileMessage(Filename, recent);

        await WeakReferenceMessenger.Default.Send(message);

        await message.Response;
    }
}