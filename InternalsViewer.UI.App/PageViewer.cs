using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App;

public partial class PageViewer : Form
{
    public PageViewer()
    {
        InitializeComponent();
    }

    public void LoadPage(string connectionString, PageAddress pageAddress)
    {
        pageViewerWindow1.LoadPage(connectionString, pageAddress);
    }
}
