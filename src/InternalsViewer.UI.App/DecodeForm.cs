namespace InternalsViewer.UI.App;

public partial class DecodeForm : Form
{
    public DecodeForm()
    {
        InitializeComponent();
    }

    public void Show(PageViewerWindow pageViewerWindow)
    {
        decodeWindow.ParentWindow = pageViewerWindow;

        Show();
    }
}
