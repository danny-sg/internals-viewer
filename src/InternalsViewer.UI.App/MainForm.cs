using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers;

namespace InternalsViewer.UI.App;

public partial class MainForm : Form
{
    public IServerInfoProvider ServerInfoProvider { get; }

    public IDatabaseLoader DatabaseLoader { get; }

    public IPageService PageService { get; }

    public IRecordService RecordService { get; }

    public CurrentConnection Connection { get; }

    public MainForm(IServerInfoProvider serverInfoProvider,
                    IDatabaseLoader databaseLoader,
                    IPageService pageService,
                    IRecordService recordService,
                    CurrentConnection connection)
    {
        ServerInfoProvider = serverInfoProvider;
        DatabaseLoader = databaseLoader;
        PageService = pageService;
        RecordService = recordService;
        Connection = connection;

        InitializeComponent();
    }

    private void AllocationWindowOnConnect(object sender, EventArgs e)
    {
        var connectionForm = new ConnectionForm();

        connectionForm.FormClosed += async (_, _) =>
        {
            if (connectionForm.DialogResult == DialogResult.OK)
            {
                Connection.ConnectionString = connectionForm.ConnectionString;

                var databases = await ServerInfoProvider.GetDatabases();

                Connection.DatabaseName =databases.FirstOrDefault()?.Name ?? "master";

                allocationWindow.Databases = databases;
                
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
