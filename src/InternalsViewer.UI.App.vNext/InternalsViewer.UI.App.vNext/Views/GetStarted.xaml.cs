using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class GetStarted
{
    public GetStarted()
    {
        InitializeComponent();
    }

    public GetStartedViewModel ViewModel => (GetStartedViewModel)DataContext;   
}
