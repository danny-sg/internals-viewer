using InternalsViewer.UI.App.ViewModels.Index;

namespace InternalsViewer.UI.App.Views;

public sealed partial class IndexView
{
    public IndexTabViewModel ViewModel => (IndexTabViewModel)DataContext;

    public IndexView()
    {
        InitializeComponent();
    }
}
