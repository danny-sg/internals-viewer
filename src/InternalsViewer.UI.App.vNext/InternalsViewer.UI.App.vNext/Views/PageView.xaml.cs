using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class PageView
{
    public DatabaseViewModel ViewModel => (DatabaseViewModel)DataContext;

    public PageView()
    {
        InitializeComponent();
    }
}
