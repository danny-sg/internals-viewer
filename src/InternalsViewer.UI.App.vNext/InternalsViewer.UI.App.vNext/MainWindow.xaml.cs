

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.UI.App.vNext.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;

namespace InternalsViewer.UI.App.vNext;

public sealed partial class MainWindow: Window
{
    public required MainViewModel ViewModel { get; set; }

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        ExtendsContentIntoTitleBar = true;
        
        SetTitleBar(CustomDragRegion);

        ViewModel = new MainViewModel(serviceProvider);
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
