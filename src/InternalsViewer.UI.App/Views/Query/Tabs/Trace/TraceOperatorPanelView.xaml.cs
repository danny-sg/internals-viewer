using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

/// <summary>
/// One operator, each of its two inputs above the rows that input holds
/// </summary>
/// <remarks>
/// An operator is a single document, so the panes are laid out here rather than as dock nodes - they belong to the operator and cannot be
/// moved or closed on their own. An input that is another operator shows that operator's results alone, its own inputs being a tab of
/// their own.
/// </remarks>
public sealed partial class TraceOperatorPanelView : UserControl
{
    public TraceOperatorViewModel? ViewModel => DataContext as TraceOperatorViewModel;

    public TraceOperatorPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => ApplyPanes();
    }

    private TraceOperatorViewModel? _appliedViewModel;

    /// <summary>
    /// Builds the panes once for an operator, leaving the sizes its splitters have since been dragged to
    /// </summary>
    /// <remarks>
    /// The view is kept alive and rehosted each time tabs are switched, which raises DataContextChanged again. Building again on that would
    /// put fresh views in the panes, losing the index map each had loaded, and return every row and column to the share of the space it
    /// started with.
    /// </remarks>
    private void ApplyPanes()
    {
        if (ViewModel is not { } viewModel || ReferenceEquals(viewModel, _appliedViewModel))
        {
            return;
        }

        _appliedViewModel = viewModel;

        Fill(OuterTopHost, OuterHeader, viewModel.OuterTop);
        Fill(InnerTopHost, InnerHeader, viewModel.InnerTop);

        Fill(OuterBottomHost, header: null, viewModel.OuterBottom);
        Fill(InnerBottomHost, header: null, viewModel.InnerBottom);

        OutputHeader.Text = $"Output ({viewModel.NodeId})";

        OutputHost.Content = new TraceRowStreamPanelView { DataContext = viewModel.Output };

        Collapse(OuterBottomRow, OuterSplitter, viewModel.OuterBottom);
        Collapse(InnerBottomRow, InnerSplitter, viewModel.InnerBottom);

        var hasInner = viewModel.InnerTop.Kind != TracePaneKind.Empty;

        InnerColumn.Width = hasInner ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        InputSplitter.Visibility = hasInner ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void Fill(ContentControl host, TextBlock? header, TracePane pane)
    {
        host.Content = CreateView(pane);

        if (header is null)
        {
            return;
        }

        header.Text = pane.Title;
        header.Visibility = string.IsNullOrEmpty(header.Text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void Collapse(RowDefinition row, FrameworkElement splitter, TracePane pane)
    {
        var isVisible = pane.Kind != TracePaneKind.Empty;

        row.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        splitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static FrameworkElement? CreateView(TracePane pane)
    {
        FrameworkElement? view = pane.Kind switch
        {
            TracePaneKind.Visual => new TraceVisualPanelView(),
            TracePaneKind.RowStream => new TraceRowStreamPanelView(),
            TracePaneKind.HeldRows => new TraceHeldRowsPanelView(),
            TracePaneKind.HashTable => new TraceHashTablePanelView(),
            _ => null
        };

        if (view is not null)
        {
            view.DataContext = pane.Content;
        }

        return view;
    }
}
