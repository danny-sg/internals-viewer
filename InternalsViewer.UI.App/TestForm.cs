using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI.App;

public partial class TestForm : Form
{
    public TestForm()
    {
        InitializeComponent();
    }

    private void AllocationWindow_Connect(object sender, EventArgs e)
    {
        InternalsViewerConnection.CurrentConnection().SetCurrentServer("localhost", true, string.Empty, string.Empty);

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
