using InternalsViewer.Internals;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Providers;

namespace InternalsViewer.UI.App;

public partial class TestForm : Form
{
    public IDatabaseInfoProvider DatabaseInfoProvider { get; }

    public CurrentConnection Connection { get; }

    public TestForm(IDatabaseInfoProvider databaseInfoProvider, CurrentConnection connection)
    {
        DatabaseInfoProvider = databaseInfoProvider;
        Connection = connection;

        InitializeComponent();
    }

    private async void AllocationWindow_Connect(object sender, EventArgs e)
    {
        Connection.ConnectionString = @"Data Source=localhost;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True";
        Connection.DatabaseName = "AdventureWorks2022";

        var databases = await DatabaseInfoProvider.GetDatabases();
        
        allocationWindow.RefreshDatabases();
    }

    private void allocationWindow_ViewPage(object? sender, PageEventArgs e)
    {
        var connectionString = InternalsViewerConnection.CurrentConnection().ConnectionString;

        var window = new PageViewer();
        window.LoadPage(connectionString, e.Address);

        window.Show();
    }
}
