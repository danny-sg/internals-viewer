using InternalsViewer.UI.App.vNext.ViewModels.Connections;
using Microsoft.UI.Xaml.Navigation;

namespace InternalsViewer.UI.App.vNext.Views.Connect;

public sealed partial class ConnectServerPage
{
    public ConnectServerPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Enabled;
    }

    public ServerConnectionViewModel ViewModel => (ServerConnectionViewModel)DataContext;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ServerConnectionViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }

    private async void DatabaseComboBox_DropDownOpened(object? sender, object e)
    {
        await ViewModel.RefreshDatabases();
    }
}
