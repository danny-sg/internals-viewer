using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

/// <summary>
/// The rows one side of an operator is holding, shown under the object they were read from
/// </summary>
public sealed partial class TraceSideRecordsPanelView : UserControl
{
    public TraceVisualViewModel? ViewModel => DataContext as TraceVisualViewModel;

    public TraceSideRecordsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
