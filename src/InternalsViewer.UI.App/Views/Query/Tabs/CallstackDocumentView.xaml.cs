using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class CallstackDocumentView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public CallstackDocumentView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();

        CallstackGrid.LoadingRow += (_, e) => e.Row.Transitions.Clear();
    }
}
