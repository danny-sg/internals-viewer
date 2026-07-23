using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InternalsViewer.UI.App.Tests.ViewModels.Connections;

public class ConnectServerViewModelTests
{
    [Fact]
    public void Can_Connect_With_An_Instance_And_Database()
    {
        var viewModel = Create();

        viewModel.Database = "master";

        Assert.True(viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void Cannot_Connect_Without_An_Instance_Name()
    {
        var viewModel = Create();

        viewModel.Database = "master";

        viewModel.InstanceName = string.Empty;

        Assert.False(viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void Cannot_Connect_Without_A_Database()
    {
        var viewModel = Create();

        viewModel.InstanceName = "server1";

        Assert.False(viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void Cannot_Connect_While_Busy()
    {
        var viewModel = Create();

        viewModel.Database = "master";

        viewModel.IsBusy = true;

        Assert.False(viewModel.ConnectCommand.CanExecute(null));
    }

#pragma warning disable CS0618
    [Theory]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated, false, false)]
    [InlineData(SqlAuthenticationMethod.SqlPassword, true, true)]
    [InlineData(SqlAuthenticationMethod.ActiveDirectoryPassword, true, true)]
#pragma warning restore CS0618
    public void Enables_Credential_Fields_By_Authentication_Type(SqlAuthenticationMethod method,
                                                                 bool userIdEnabled,
                                                                 bool passwordEnabled)
    {
        var viewModel = Create();

        viewModel.AuthenticationType = (int)method;

        Assert.Equal(userIdEnabled, viewModel.IsUserIdEnabled);
        Assert.Equal(passwordEnabled, viewModel.IsPasswordEnabled);
    }

    [Fact]
    public void Notifies_Credential_Field_Enablement_When_The_Authentication_Type_Changes()
    {
        var viewModel = Create();

        var raised = new List<string?>();

        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.AuthenticationType = (int)SqlAuthenticationMethod.SqlPassword;

        Assert.Contains(nameof(ConnectServerViewModel.IsUserIdEnabled), raised);
        Assert.Contains(nameof(ConnectServerViewModel.IsPasswordEnabled), raised);
    }

    private static ConnectServerViewModel Create()
        => new(new SettingsService(NullLogger<SettingsService>.Instance, Options.Create(new SettingsOptions())));
}
