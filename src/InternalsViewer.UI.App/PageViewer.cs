using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.UI.App;

public partial class PageViewer : Form
{
    public IPageService PageService { get; }

    public IRecordService RecordService { get; }

    public DatabaseSource Database { get; }

    public PageViewer(IPageService pageService, IRecordService recordService, DatabaseSource database)
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
      
        pageViewerWindow.PageService = PageService;

        await pageViewerWindow.LoadPage(pageAddress);

        ResumeLayout();
    }

    private void PageViewerWindow_OpenDecodeWindow(object? sender, EventArgs e)
    {
        var decodeForm = new DecodeForm();

        decodeForm.Show(pageViewerWindow);
    }
}
