

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.UI.Composition.SystemBackdrops;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.Internals.Providers;
using InternalsViewer.UI.App.vNext.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.vNext;

public sealed partial class MainWindow
{
    public IDatabaseLoader DatabaseLoader { get; }

    public CurrentConnection Connection { get; }

    public required MainViewModel ViewModel { get; set; }

    public MainWindow(IDatabaseLoader databaseLoader, CurrentConnection connection)
    {
        DatabaseLoader = databaseLoader;
        Connection = connection;

        InitializeComponent();

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        ExtendsContentIntoTitleBar = true;
        
        SetTitleBar(CustomDragRegion);

        ViewModel = new MainViewModel(databaseLoader, Connection);
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        ViewModel.CloseTabCommand.Execute(args.Item);
    }
}
