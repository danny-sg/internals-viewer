using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.UI.App;

public partial class PageViewer : Form
{
    public IPageService PageService { get; }

    public IRecordService RecordService { get; }

    public DatabaseDetail DatabaseDetail { get; }

    public PageViewer(IPageService pageService, IRecordService recordService, DatabaseDetail databaseDetail)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer,
                 true);

        PageService = pageService;
        RecordService = recordService;
        DatabaseDetail = databaseDetail;

        InitializeComponent();
    }

    public async Task LoadPage(PageAddress pageAddress)
    {
        SuspendLayout();

        pageViewerWindow.DatabaseDetail = DatabaseDetail;

        await pageViewerWindow.LoadPage(pageAddress);

        ResumeLayout();
    }

    private void PageViewerWindow_OpenDecodeWindow(object? sender, EventArgs e)
    {
        var decodeForm = new DecodeForm();

        decodeForm.Show(pageViewerWindow);
    }
}
