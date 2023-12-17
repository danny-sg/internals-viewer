using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.UI.App;

public partial class PageViewer : Form
{
    public IPageService PageService { get; }

    public IRecordService RecordService { get; }

    public Database Database { get; }

    public PageViewer(IPageService pageService, IRecordService recordService, Database database)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer,
                 true);

        PageService = pageService;
        RecordService = recordService;
        Database = database;

        InitializeComponent();
    }

    public async Task LoadPage(PageAddress pageAddress)
    {
        SuspendLayout();

        pageViewerWindow.Database = Database;

        await pageViewerWindow.LoadPage(pageAddress);

        ResumeLayout();
    }

    private void PageViewerWindow_OpenDecodeWindow(object? sender, EventArgs e)
    {
        var decodeForm = new DecodeForm();

        decodeForm.Show(pageViewerWindow);
    }
}
