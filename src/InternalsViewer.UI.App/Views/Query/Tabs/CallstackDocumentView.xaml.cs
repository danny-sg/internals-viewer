using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Dock document showing the callstack frames captured for the active query.</summary>
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
