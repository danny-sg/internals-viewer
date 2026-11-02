using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InternalsViewer.UI.App.Tests.ViewModels.Connections;

[Trait("Category", "Unit")]
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

    [Fact]
    public void Cannot_Refresh_Databases_While_Busy()
    {
        var viewModel = Create();

        Assert.True(viewModel.RefreshDatabasesCommand.CanExecute(null));

        viewModel.IsBusy = true;

        Assert.False(viewModel.RefreshDatabasesCommand.CanExecute(null));
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

    [Fact]
    public void Connection_String_Includes_Credentials_For_Sql_Server_Authentication()
    {
        var viewModel = CreateWithCredentials();

        var builder = new SqlConnectionStringBuilder(viewModel.GetConnectionString());

        Assert.Equal("user1", builder.UserID);
        Assert.Equal("password1", builder.Password);
    }

    [Fact]
    public void Connection_String_Drops_Credentials_After_Switching_To_Windows_Authentication()
    {
        var viewModel = CreateWithCredentials();

        viewModel.GetConnectionString();

        viewModel.AuthenticationType = (int)SqlAuthenticationMethod.ActiveDirectoryIntegrated;

        var builder = new SqlConnectionStringBuilder(viewModel.GetConnectionString());

        Assert.Equal(string.Empty, builder.UserID);
        Assert.Equal(string.Empty, builder.Password);
    }

    [Fact]
    public void Connection_String_Drops_A_Cleared_Password()
    {
        var viewModel = CreateWithCredentials();

        viewModel.GetConnectionString();

        viewModel.Password = string.Empty;

        var builder = new SqlConnectionStringBuilder(viewModel.GetConnectionString());

        Assert.Equal("user1", builder.UserID);
        Assert.Equal(string.Empty, builder.Password);
    }

    [Fact]
    public void Database_List_Keeps_The_Database_When_The_Selection_Is_Cleared()
    {
        var viewModel = Create();

        viewModel.Database = "Sales";

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConnectServerViewModel.Databases))
            {
                viewModel.Database = null;
            }
        };

        viewModel.SetDatabaseList(["master", "model"]);

        Assert.Equal("Sales", viewModel.Database);
        Assert.Equal(["Sales", "master", "model"], viewModel.Databases);
    }

    [Fact]
    public void Database_List_Keeps_The_Database_When_The_List_Is_Empty()
    {
        var viewModel = Create();

        viewModel.Database = "Sales";

        viewModel.SetDatabaseList([]);

        Assert.Equal("Sales", viewModel.Database);
        Assert.Equal(["Sales"], viewModel.Databases);
    }

    private static ConnectServerViewModel CreateWithCredentials()
    {
        var viewModel = Create();

        viewModel.AuthenticationType = (int)SqlAuthenticationMethod.SqlPassword;
        viewModel.UserId = "user1";
        viewModel.Password = "password1";

        return viewModel;
    }

    private static ConnectServerViewModel Create()
        => new(NullLogger<ConnectServerViewModel>.Instance,
               new SettingsService(NullLogger<SettingsService>.Instance, Options.Create(new SettingsOptions())));
}
