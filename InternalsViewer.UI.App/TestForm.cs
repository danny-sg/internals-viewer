using InternalsViewer.Internals;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Providers;

namespace InternalsViewer.UI.App;

public partial class TestForm : Form
{
    public IDatabaseInfoProvider DatabaseInfoProvider { get; }

    public IDatabaseService DatabaseService { get; }

    public IPageService PageService { get; }

    public CurrentConnection Connection { get; }

    public TestForm(IDatabaseInfoProvider databaseInfoProvider, IDatabaseService databaseService, IPageService pageService, CurrentConnection connection)
    {
        DatabaseInfoProvider = databaseInfoProvider;
        DatabaseService = databaseService;
        PageService = pageService;
        Connection = connection;

        InitializeComponent();
    }

    private async void AllocationWindow_Connect(object sender, EventArgs e)
    {
        Connection.ConnectionString = @"Data Source=localhost;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True";
        Connection.DatabaseName = "AdventureWorks2022";

        var databases = await DatabaseInfoProvider.GetDatabases();

        allocationWindow.Databases = databases;
        allocationWindow.RefreshDatabases();
    }

    private void allocationWindow_ViewPage(object? sender, PageEventArgs e)
    {
        if(allocationWindow.CurrentDatabase!=null)
        {
            var window = new PageViewer(PageService, allocationWindow.CurrentDatabase);

            window.LoadPage(e.Address);

            window.Show();
        }
       
    }
}
