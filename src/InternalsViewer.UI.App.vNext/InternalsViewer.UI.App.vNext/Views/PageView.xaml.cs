using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class PageView
{
    public DatabaseViewModel ViewModel => (DatabaseViewModel)DataContext;

    public PageView()
    {
        InitializeComponent();
    }
}
