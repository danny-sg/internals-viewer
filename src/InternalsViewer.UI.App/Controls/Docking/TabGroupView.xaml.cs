using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace InternalsViewer.UI.App.Controls.Docking;

/// <summary>
/// Renders a single <see cref="TabGroupNode"/> as a <see cref="TabView"/> and provides the
/// VS Code-style drop overlay that lets a dragged tab be moved into this group or split it
/// left/right/top/bottom. Tab items are built in code (rather than via TabItemsSource) so each
/// document control is hosted directly as a tab's content, which lays out full-size.
/// </summary>
public sealed partial class TabGroupView : UserControl
{
    private const double EdgeFraction = 0.25;

    // Height of the tab strip the overlay sits over; drops here move into the group rather than split it.
    private const double TabStripReserve = 44;

    private readonly Dictionary<DocumentViewModel, TabViewItem> items = new();

    private bool syncing;

    public TabGroupNode? Group { get; private set; }

    public DockLayoutViewModel? Dock { get; private set; }

    public TabGroupView()
    {
        InitializeComponent();

        Unloaded += OnUnloaded;
    }

    public void Initialize(TabGroupNode group, DockLayoutViewModel dock)
    {
        Group = group;
        Dock = dock;

        BuildTabs();

        group.Documents.CollectionChanged += OnDocumentsChanged;
        group.PropertyChanged += OnGroupPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Group is not null)
        {
            Group.Documents.CollectionChanged -= OnDocumentsChanged;
            Group.PropertyChanged -= OnGroupPropertyChanged;
        }
    }

    private void BuildTabs()
    {
        if (Group is null)
        {
            return;
        }

        syncing = true;

        Tabs.TabItems.Clear();
        items.Clear();

        foreach (var document in Group.Documents)
        {
            var item = CreateTab(document);
            items[document] = item;
            Tabs.TabItems.Add(item);
        }

        Tabs.SelectedItem = Group.SelectedDocument is { } selected && items.TryGetValue(selected, out var selectedItem)
            ? selectedItem
            : Tabs.TabItems.Count > 0 ? Tabs.TabItems[0] : null;

        syncing = false;
    }

    private static TabViewItem CreateTab(DocumentViewModel document)
    {
        var item = new TabViewItem
        {
            Header = document.Title,
            IsClosable = document.CanClose,
            Tag = document,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        item.Content = document.CreateView();

        return item;
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => BuildTabs();

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TabGroupNode.SelectedDocument) || Group is null || syncing)
        {
            return;
        }

        if (Group.SelectedDocument is { } selected && items.TryGetValue(selected, out var item))
        {
            syncing = true;
            Tabs.SelectedItem = item;
            syncing = false;
        }
    }

    // Strip the built-in tab-switch / strip transitions so changing tabs swaps content instantly.
    private void OnTabsLoaded(object sender, RoutedEventArgs e)
    {
        if (FindDescendant<ContentPresenter>(Tabs, "TabContentPresenter") is { } content)
        {
            content.ContentTransitions = new TransitionCollection();
        }

        if (FindDescendant<ListView>(Tabs) is { } strip)
        {
            strip.ItemContainerTransitions = new TransitionCollection();
        }
    }

    private static T? FindDescendant<T>(DependencyObject root, string? name = null) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match && (name is null || match.Name == name))
            {
                return match;
            }

            if (FindDescendant<T>(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || Group is null)
        {
            return;
        }

        if (Tabs.SelectedItem is TabViewItem { Tag: DocumentViewModel document })
        {
            Group.SelectedDocument = document;
        }
    }

    private void OnTabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        if (args.Tab.Tag is not DocumentViewModel document)
        {
            return;
        }

        // Same-window drag: the document is carried via DockDragState. A text payload is still
        // required for the drag operation to be accepted by the overlay drop targets.
        args.Data.RequestedOperation = DataPackageOperation.Move;
        args.Data.SetText(document.Title);

        DockDragState.Begin(document);
    }

    private void OnTabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
        => DockDragState.End();

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is not DocumentViewModel document)
        {
            return;
        }

        // Defer: closing rebuilds this TabView's items, which must not happen while the TabView is
        // still processing its own close event (throws ArgumentException in WinRT).
        DispatcherQueue.TryEnqueue(() => Dock?.Close(document));
    }

    private void OnDropOver(object sender, DragEventArgs e)
    {
        if (!DockDragState.IsActive)
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;

        if (e.DragUIOverride is not null)
        {
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
        }

        UpdateHighlight(GetZone(e.GetPosition(RootArea)));
        e.Handled = true;
    }

    private void OnDropLeave(object sender, DragEventArgs e) => HideHighlight();

    private void OnDrop(object sender, DragEventArgs e)
    {
        var document = DockDragState.Document;

        HideHighlight();

        if (document is not null && Group is { } group && Dock is { } dock)
        {
            var zone = GetZone(e.GetPosition(RootArea));

            // Defer: the move restructures the layout and rebuilds tab strips, which must not happen
            // while the drag/drop operation is still in flight (throws ArgumentException in WinRT).
            DispatcherQueue.TryEnqueue(() => dock.Move(document, group, zone));
        }

        DockDragState.End();
        e.Handled = true;
    }

    private DropZone GetZone(Point point)
    {
        var w = RootArea.ActualWidth;
        var h = RootArea.ActualHeight;

        if (w <= 0 || h <= 0)
        {
            return DropZone.Center;
        }

        // Over the tab strip: treat as a move into this group, leaving the strip free to reorder/accept tabs.
        if (point.Y < TabStripReserve)
        {
            return DropZone.Center;
        }

        var left = point.X / w;
        var right = 1 - left;
        var top = point.Y / h;
        var bottom = 1 - top;

        var min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));

        if (min > EdgeFraction)
        {
            return DropZone.Center;
        }

        if (min == left)
        {
            return DropZone.Left;
        }

        if (min == right)
        {
            return DropZone.Right;
        }

        return min == top ? DropZone.Top : DropZone.Bottom;
    }

    private void UpdateHighlight(DropZone zone)
    {
        var w = RootArea.ActualWidth;
        var h = RootArea.ActualHeight;

        DropHighlight.Visibility = Visibility.Visible;

        switch (zone)
        {
            case DropZone.Left:
                Set(HorizontalAlignment.Left, VerticalAlignment.Stretch, w / 2, double.NaN);
                break;
            case DropZone.Right:
                Set(HorizontalAlignment.Right, VerticalAlignment.Stretch, w / 2, double.NaN);
                break;
            case DropZone.Top:
                Set(HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, h / 2);
                break;
            case DropZone.Bottom:
                Set(HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, h / 2);
                break;
            default:
                Set(HorizontalAlignment.Stretch, VerticalAlignment.Stretch, double.NaN, double.NaN);
                break;
        }
    }

    private void Set(HorizontalAlignment horizontal, VerticalAlignment vertical, double width, double height)
    {
        DropHighlight.HorizontalAlignment = horizontal;
        DropHighlight.VerticalAlignment = vertical;
        DropHighlight.Width = width;
        DropHighlight.Height = height;
    }

    private void HideHighlight() => DropHighlight.Visibility = Visibility.Collapsed;
}
