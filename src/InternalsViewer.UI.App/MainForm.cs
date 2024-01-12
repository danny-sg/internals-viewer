using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.UI.App;

public partial class MainForm : Form
{
    public IServerInfoProvider ServerInfoProvider { get; }

    public IDatabaseLoader DatabaseLoader { get; }

    public IPageService PageService { get; }

    public IRecordService RecordService { get; }

    public MainForm(IServerInfoProvider serverInfoProvider,
                    IDatabaseLoader databaseLoader,
                    IPageService pageService,
                    IRecordService recordService)
    {
        ServerInfoProvider = serverInfoProvider;
        DatabaseLoader = databaseLoader;
        PageService = pageService;
        RecordService = recordService;

        InitializeComponent();
    }

    private void AllocationWindowOnConnect(object sender, EventArgs e)
    {
        var connectionForm = new ConnectionForm();

        connectionForm.FormClosed += async (_, _) =>
        {
            if (connectionForm.DialogResult == DialogResult.OK)
            {
                var connectionString = connectionForm.ConnectionString;

                var databases = await ServerInfoProvider.GetDatabases(connectionString);

                allocationWindow.Databases = databases;

                var connection = ServerConnectionFactory.Create(c => c.ConnectionString = connectionString);

                allocationWindow.CurrentDatabase = new Internals.Engine.Database.DatabaseSource(connection);
                allocationWindow.RefreshDatabases();
            }
        };

        connectionForm.ShowDialog();
    }

    private async void allocationWindow_ViewPage(object? sender, PageEventArgs e)
    {
        if (allocationWindow.CurrentDatabase != null)
        {
            var window = new PageViewer(PageService, RecordService, allocationWindow.CurrentDatabase);

            window.Show();

            await window.LoadPage(e.Address);
        }
    }
}
