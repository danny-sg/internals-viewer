using InternalsViewer.Mcp.Clients.Enums;
using InternalsViewer.Mcp.Clients;
using InternalsViewer.Mcp.Hosting;
using InternalsViewer.Mcp.Interfaces.Clients;
using InternalsViewer.UI.App.ViewModels.Mcp;

namespace InternalsViewer.UI.App.Tests.ViewModels.Mcp;

[Trait("Category", "Unit")]
public class McpClientViewModelTests
{
    private static readonly McpHostSettings Settings = new(8484, "secret");

    [Fact]
    public async Task Refresh_Shows_The_Client_State()
    {
        var client = new FakeMcpClient { State = new McpClientState(McpClientStatus.NeedsUpdate, "Registered with a previous token") };

        var viewModel = new McpClientViewModel(client, () => Settings);

        await viewModel.RefreshAsync();

        Assert.Equal(McpClientStatus.NeedsUpdate, viewModel.Status);
        Assert.Equal("Registered with a previous token", viewModel.StatusText);
        Assert.Equal("Update", viewModel.RegisterLabel);
        Assert.True(viewModel.IsRegistered);
        Assert.True(viewModel.RegisterCommand.CanExecute(null));
        Assert.True(viewModel.UnregisterCommand.CanExecute(null));
    }

    [Fact]
    public async Task Register_Passes_The_Current_Settings_And_Refreshes()
    {
        var client = new FakeMcpClient { State = new McpClientState(McpClientStatus.NotRegistered, "Not registered") };

        var viewModel = new McpClientViewModel(client, () => Settings);

        await viewModel.RefreshAsync();

        Assert.Equal("Register", viewModel.RegisterLabel);
        Assert.False(viewModel.UnregisterCommand.CanExecute(null));

        client.State = new McpClientState(McpClientStatus.Registered, "Registered");

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal(Settings, Assert.Single(client.Registrations));
        Assert.Equal(McpClientStatus.Registered, viewModel.Status);
        Assert.False(viewModel.RegisterCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_Failed_Registration_Shows_The_Reason()
    {
        var client = new FakeMcpClient
        {
            State = new McpClientState(McpClientStatus.NotRegistered, "Not registered"),
            Failure = new McpClientException("claude returned exit code 1")
        };

        var viewModel = new McpClientViewModel(client, () => Settings);

        await viewModel.RefreshAsync();

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("claude returned exit code 1", viewModel.StatusText);
        Assert.Equal(McpClientStatus.NotRegistered, viewModel.Status);
    }

    [Fact]
    public async Task Update_If_Registered_Leaves_Unregistered_Clients_Alone()
    {
        var client = new FakeMcpClient { State = new McpClientState(McpClientStatus.NotRegistered, "Not registered") };

        var viewModel = new McpClientViewModel(client, () => Settings);

        await viewModel.RefreshAsync();

        await viewModel.UpdateIfRegisteredAsync();

        Assert.Empty(client.Registrations);
    }

    private sealed class FakeMcpClient : IMcpClient
    {
        public McpClientState State { get; set; } = new(McpClientStatus.NotInstalled, "Not installed");

        public Exception? Failure { get; set; }

        public List<McpHostSettings> Registrations { get; } = [];

        public string Id => "fake";

        public string Name => "Fake Client";

        public string Description => "A client for tests";

        public string? HelpUrl => null;

        public bool IsInstalled => State.Status != McpClientStatus.NotInstalled;

        public Task<McpClientState> GetStateAsync(McpHostSettings settings, CancellationToken cancellationToken) => Task.FromResult(State);

        public Task RegisterAsync(McpHostSettings settings, CancellationToken cancellationToken)
        {
            if (Failure is { } failure)
            {
                throw failure;
            }

            Registrations.Add(settings);

            return Task.CompletedTask;
        }

        public Task UnregisterAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
