using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

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
            ViewModel?.RequestPlayhead(writer.First.Point);
        }
    }

    private void OnAddressContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (ViewModel is { Navigator: { } navigator, Details.BlockAddress: not 0 and var address })
        {
            TraceLinkMenu.Show(TraceLinkMenu.CreateForAddress(navigator, address), sender, e);
        }
    }

    private void OnTypeContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (ViewModel is not { Navigator: { } navigator } viewModel)
        {
            return;
        }

        var menu = new MenuFlyout();

        TraceLinkMenu.AddSearch(menu, navigator, viewModel.Details.Type);

        TraceLinkMenu.Show(menu, sender, e);
    }

    private void OnInitialisedContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowLinkMenu(ViewModel?.Details.InitialisedLink, sender, e);

    private void OnInitialisedByContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (ViewModel is not { Navigator: { } navigator, Details: { InitialisedLink: { } link } details })
        {
            return;
        }

        var menu = TraceLinkMenu.CreateForFunction(navigator, link);

        TraceLinkMenu.AddSearch(menu, navigator, details.InitialisedBy);

        TraceLinkMenu.Show(menu, sender, e);
    }

    private void OnDestroyedContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowLinkMenu(ViewModel?.Details.DestroyedLink, sender, e);

    private void OnWriterContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (ViewModel is not { Navigator: { } navigator } viewModel
            || sender is not FrameworkElement { DataContext: MemoryWriterSummary writer })
        {
            return;
        }

        var menu = TraceLinkMenu.CreateForFunction(navigator, writer.First, isFirst: writer.Count > 1);

        TraceLinkMenu.AddSearch(menu, navigator, writer.Label);

        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(MenuItems.Create("Show History", () => viewModel.ShowHistory(writer.Label, null)));

        TraceLinkMenu.Show(menu, sender, e);
    }

    private void OnCopyValueClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string { Length: > 0 } text)
        {
            ClipboardText.Copy(text);
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
            ViewModel?.MarkWriter(writer.Label);
        }
    }

    private void OnWriterPointerExited(object sender, PointerRoutedEventArgs e) => ViewModel?.MarkWriter(null);

    private void ShowLinkMenu(TraceCallLink? link, UIElement sender, ContextRequestedEventArgs e)
    {
        if (ViewModel?.Navigator is { } navigator && link is not null)
        {
            TraceLinkMenu.Show(TraceLinkMenu.CreateForPosition(navigator, link), sender, e);
        }
    }
}
