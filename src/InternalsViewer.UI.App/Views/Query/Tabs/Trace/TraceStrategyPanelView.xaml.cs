using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceStrategyPanelView : UserControl
{
    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

    public TraceStrategyPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public Visibility SectionHeaderVisibility(TraceKind kind)
        => kind is TraceKind.KeyLookup or TraceKind.MergeJoin ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InnerPendingVisibility(TraceKind kind, AccessStrategy? innerStrategy)
        => kind == TraceKind.KeyLookup && innerStrategy is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InnerStrategyVisibility(AccessStrategy? innerStrategy)
        => innerStrategy is null ? Visibility.Collapsed : Visibility.Visible;
}
