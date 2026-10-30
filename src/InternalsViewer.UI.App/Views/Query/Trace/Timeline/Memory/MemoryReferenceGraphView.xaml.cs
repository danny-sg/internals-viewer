using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReferenceGraphView : UserControl
{
    public MemoryReferenceGraphView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public MemoryReferenceGraphViewModel? ViewModel => DataContext as MemoryReferenceGraphViewModel;
}
