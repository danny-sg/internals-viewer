using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.UI.App;

public partial class PageViewer : Form
{
    public IPageService PageService { get; }

    public Database Database { get; }

    public PageViewer(IPageService pageService, Database database)
    {
        PageService = pageService;
        Database = database;
        InitializeComponent();
    }

    public void LoadPage(PageAddress pageAddress)
    {
        pageViewerWindow.Database = Database;

        pageViewerWindow.LoadPage(pageAddress);
    }
}
