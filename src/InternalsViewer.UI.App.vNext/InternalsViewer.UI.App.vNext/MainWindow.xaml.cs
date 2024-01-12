

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.UI.App.vNext.ViewModels;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.vNext.Services;

namespace InternalsViewer.UI.App.vNext;

public sealed partial class MainWindow: Window
{
    public IDatabaseLoader DatabaseLoader { get; }
    
    public SettingsService SettingsService { get; }

    public required MainViewModel ViewModel { get; set; }

    public MainWindow(IDatabaseLoader databaseLoader, SettingsService settingsService)
    {
        DatabaseLoader = databaseLoader;
        SettingsService = settingsService;

        InitializeComponent();

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        ExtendsContentIntoTitleBar = true;
        
        SetTitleBar(CustomDragRegion);

        ViewModel = new MainViewModel(DatabaseLoader, SettingsService);
    }

    public async Task InitializeAsync()
    {
        await ViewModel.InitializeAsync();
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        ViewModel.CloseTabCommand.Execute(args.Item);
    }
}
