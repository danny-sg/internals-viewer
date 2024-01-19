using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.vNext.Controls.Page;

public sealed partial class MarkerListView
{
    public PageViewModel ViewModel => (PageViewModel)DataContext;

    public MarkerListView()
    {
        InitializeComponent();
    }
}
