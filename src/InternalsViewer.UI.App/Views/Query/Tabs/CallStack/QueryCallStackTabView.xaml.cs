using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class QueryCallStackTabView : UserControl
{
    public QueryCallStackTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;
}
