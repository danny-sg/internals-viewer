using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryHashTableView : UserControl
{
    public MemoryHashTableView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public MemoryHashTableViewModel? ViewModel => DataContext as MemoryHashTableViewModel;
}
