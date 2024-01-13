using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class PageView
{
    public PageViewModel ViewModel => (PageViewModel)DataContext;

    public PageView()
    {
        InitializeComponent();
    }
}
