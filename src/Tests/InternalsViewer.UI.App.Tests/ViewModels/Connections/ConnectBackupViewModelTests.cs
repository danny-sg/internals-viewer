using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Connections;

namespace InternalsViewer.UI.App.Tests.ViewModels.Connections;

public sealed class ConnectBackupViewModelTests : IDisposable
{
    private readonly List<string> tempFiles = [];

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<ConnectBackupMessage>(this);

        foreach (var file in tempFiles)
        {
            File.Delete(file);
        }
    }

    private string CreateTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bak");

        File.WriteAllBytes(path, [0]);

        tempFiles.Add(path);

        return path;
    }

    [Fact]
    public void Adding_Existing_Files_Makes_The_View_Model_Valid()
    {
        var viewModel = new ConnectBackupViewModel();

        Assert.False(viewModel.IsValid);

        viewModel.AddFiles([CreateTempFile(), CreateTempFile()]);

        Assert.True(viewModel.IsValid);
        Assert.Equal(2, viewModel.Filenames.Count);
    }

    [Fact]
    public void Adding_A_Missing_File_Makes_The_View_Model_Invalid()
    {
        var viewModel = new ConnectBackupViewModel();

        viewModel.AddFiles([CreateTempFile(), @"C:\DoesNotExist\Missing.bak"]);

        Assert.False(viewModel.IsValid);
    }

    [Fact]
    public void Duplicate_Files_Are_Only_Added_Once()
    {
        var viewModel = new ConnectBackupViewModel();

        var path = CreateTempFile();

        viewModel.AddFiles([path, path]);
        viewModel.AddFiles([path]);

        Assert.Single(viewModel.Filenames);
    }

    [Fact]
    public void Removing_The_Last_File_Makes_The_View_Model_Invalid()
    {
        var viewModel = new ConnectBackupViewModel();

        var path = CreateTempFile();

        viewModel.AddFiles([path]);

        Assert.True(viewModel.IsValid);

        viewModel.RemoveFileCommand.Execute(path);

        Assert.False(viewModel.IsValid);
        Assert.Empty(viewModel.Filenames);
    }

    [Fact]
    public async Task Failed_Connect_Shows_The_Error_Message_From_The_Reply()
    {
        var viewModel = new ConnectBackupViewModel();

        viewModel.AddFiles([CreateTempFile()]);

        WeakReferenceMessenger.Default.Register<ConnectBackupMessage>(this, (_, m) =>
        {
            m.ErrorMessage = "The backup is striped across 4 files but 1 were provided.";

            m.Reply(false);
        });

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("striped across 4 files", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Adding_A_File_Clears_The_Previous_Error()
    {
        var viewModel = new ConnectBackupViewModel();

        viewModel.AddFiles([CreateTempFile()]);

        WeakReferenceMessenger.Default.Register<ConnectBackupMessage>(this, (_, m) =>
        {
            m.ErrorMessage = "Error";

            m.Reply(false);
        });

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);

        viewModel.AddFiles([CreateTempFile()]);

        Assert.False(viewModel.HasError);
    }
}
