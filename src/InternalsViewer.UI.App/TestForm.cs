using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Providers;

namespace InternalsViewer.UI.App;

public partial class TestForm : Form
{
    public IServerInfoProvider ServerInfoProvider { get; }

    public IDatabaseLoader DatabaseLoader { get; }

    public IPageLoader PageLoader { get; }

    public IRecordService RecordService { get; }

    public CurrentConnection Connection { get; }

    public TestForm(IServerInfoProvider serverInfoProvider,
                    IDatabaseLoader databaseLoader,
                    IPageLoader pageLoader,
                    IRecordService recordService,
                    CurrentConnection connection)
    {
        ServerInfoProvider = serverInfoProvider;
        DatabaseLoader = databaseLoader;
        PageLoader = pageLoader;
        RecordService = recordService;
        Connection = connection;

        InitializeComponent();
    }

    private void AllocationWindow_Connect(object sender, EventArgs e)
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
            var window = new PageViewer(PageLoader, RecordService, allocationWindow.CurrentDatabase);

            window.Show();

            await window.LoadPage(e.Address);
        }
    }
}
