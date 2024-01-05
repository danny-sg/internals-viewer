

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.ApplicationModel.Core;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.Internals.Providers;

namespace InternalsViewer.UI.App.vNext;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public IDatabaseLoader DatabaseLoader { get; }

    public CurrentConnection Connection { get; }

    public MainWindow(IDatabaseLoader databaseLoader, CurrentConnection connection)
    {
        DatabaseLoader = databaseLoader;
        Connection = connection;

        InitializeComponent();

        DatabaseView.DatabaseLoader = DatabaseLoader;
        DatabaseView.Connection = Connection;
        SystemBackdrop = new MicaBackdrop()
            { Kind = MicaKind.Base };

        ExtendsContentIntoTitleBar = true;
        
        SetTitleBar(CustomDragRegion);
    }
}
