using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.UI.Xaml.Navigation;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectServerPage
{
    public ConnectServerPage()
    {
        InitializeComponent();

        NavigationCacheMode = NavigationCacheMode.Enabled;
    }

    private ConnectServerViewModel ViewModel => (ConnectServerViewModel)DataContext;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConnectServerViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }

    private async void DatabaseComboBox_DropDownOpened(object? sender, object e)
    {
        await ViewModel.RefreshDatabases();
    }
}
