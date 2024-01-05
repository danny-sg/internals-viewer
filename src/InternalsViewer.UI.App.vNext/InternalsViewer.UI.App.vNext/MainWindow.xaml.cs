

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.UI.Composition.SystemBackdrops;
using Windows.ApplicationModel.Core;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.vNext;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop()
            { Kind = MicaKind.Base };

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CustomDragRegion);
    }

    private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
    {
        CustomDragRegion.MinWidth = sender.SystemOverlayRightInset;
        ShellTitlebarInset.MinWidth = sender.SystemOverlayLeftInset;

        CustomDragRegion.Height = ShellTitlebarInset.Height = sender.Height;
    }
}
