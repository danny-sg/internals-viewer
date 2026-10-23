using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class MemoryDetailsPanelView : UserControl
{
    public MemoryDetailsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private void OnInitialisedClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { Details.InitialisedAt: { } point } viewModel)
        {
            viewModel.RequestPlayhead(point);
        }
    }

    private void OnDestroyedClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { Details.DestroyedAt: { } point } viewModel)
        {
            viewModel.RequestPlayhead(point);
        }
    }

    private void OnWriterClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { DataContext: MemoryWriterSummary writer })
        {
            ViewModel?.ShowHistory(writer.Label, null);
        }
    }

    private void OnInitialisedPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.MarkPosition(viewModel.Details.InitialisedAt);
        }
    }

    private void OnDestroyedPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.MarkPosition(viewModel.Details.DestroyedAt);
        }
    }

    private void OnMarkPointerExited(object sender, PointerRoutedEventArgs e) => ViewModel?.MarkPosition(null);

    private void OnWriterPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is HyperlinkButton { DataContext: MemoryWriterSummary writer })
        {
            ViewModel?.HighlightWriter(writer.Label);
        }
    }

    private void OnWriterPointerExited(object sender, PointerRoutedEventArgs e) => ViewModel?.HighlightWriter(null);
}
