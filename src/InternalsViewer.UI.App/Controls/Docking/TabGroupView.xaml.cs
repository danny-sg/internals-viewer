using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Docking;

public sealed partial class TabGroupView : UserControl
{
    private const double EdgeFraction = 0.25;

    // Height of the tab strip the overlay sits over; drops here move into the group rather than split it.
    private const double TabStripReserve = 44;

    private readonly Dictionary<DocumentViewModel, TabViewItem> _items = new();

    private readonly Dictionary<DocumentViewModel, FrameworkElement> _views = new();

    private readonly List<(DocumentViewModel Document, PropertyChangedEventHandler Handler)> _headerSubscriptions = [];

    private bool _syncing;

    public TabGroupNode? Group { get; private set; }

    public DockLayoutViewModel? Dock { get; private set; }

    public TabGroupView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            DockDragState.ActiveChanged += OnDragActiveChanged;

            // Deferred to here rather than done in BuildTabs: a dock rebuild constructs the new tree while the old
            // one is still live, and the commands element cannot be moved out of a group that is still standing.
            UpdateCommands();
        };

        Unloaded += OnUnloaded;
    }

    private void OnDragActiveChanged(object? sender, EventArgs e)
    {
        if (!DockDragState.IsActive)
        {
            HideHighlight();
        }
    }

    private bool AcceptsCurrentDrag
        => DockDragState.Document is { } document && Dock?.FindGroup(document) is not null;

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
        DockDragState.ActiveChanged -= OnDragActiveChanged;

        ClearHeaderSubscriptions();

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

        _syncing = true;

        ClearHeaderSubscriptions();

        Tabs.TabItems.Clear();
        _items.Clear();

        foreach (var document in Group.Documents)
        {
            var item = CreateTab(document);
            _items[document] = item;
            Tabs.TabItems.Add(item);
        }

        SyncViews();

        Tabs.SelectedItem = Group.SelectedDocument is { } selected
                            && _items.TryGetValue(selected, out var selectedItem)
            ? selectedItem
            : Tabs.TabItems.Count > 0 ? Tabs.TabItems[0] : null;

        _syncing = false;

        ShowSelectedView();

        UpdateCommands();
    }

    private void SyncViews()
    {
        if (Group is null)
        {
            return;
        }

        foreach (var document in _views.Keys.Where(d => !Group.Documents.Contains(d)).ToList())
        {
            ContentHost.Children.Remove(_views[document]);

            _views.Remove(document);
        }
    }

    private void ShowSelectedView()
    {
        var selected = (Tabs.SelectedItem as TabViewItem)?.Tag as DocumentViewModel ?? Group?.SelectedDocument;

        if (selected is not null && !_views.ContainsKey(selected) && Group?.Documents.Contains(selected) is true)
        {
            var view = selected.CreateView();

            _views[selected] = view;

            ContentHost.Children.Add(view);
        }

        foreach (var (document, view) in _views)
        {
            view.Visibility = ReferenceEquals(document, selected) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Shows the selected document's commands in the tab strip, so a pane's own controls travel with it
    /// rather than each view carrying a toolbar row
    /// </summary>
    private void UpdateCommands()
    {
        // Until this group is in the tree the element it wants may still belong to the group being replaced, which
        // is only torn down once the rebuilt tree is in place. The Loaded handler picks it up again.
        if (!IsLoaded)
        {
            return;
        }

        // Defer: the tab change that got us here is usually one step of a larger mutation - a dock rebuild, a group
        // collapsing, a document closing - and the element cannot leave the strip it is in until that has finished.
        DispatcherQueue.TryEnqueue(HostSelectedCommands);
    }

    private void HostSelectedCommands()
    {
        if (!IsLoaded)
        {
            return;
        }

        if ((Tabs.SelectedItem as TabViewItem)?.Tag is not DocumentViewModel document)
        {
            Tabs.TabStripFooter = null;
            return;
        }

        // Filled before it is handed over: the strip's footer is a templated content slot, which takes a fresh
        // element without complaint but will not have children added to what it is already holding.
        var host = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };

        document.HostCommandsIn(host);

        Tabs.TabStripFooter = host;
    }

    private TabViewItem CreateTab(DocumentViewModel document)
    {
        var title = new TextBlock
        {
            Text = document.Title,
            Margin = new Thickness(4, 0, 4, 0),
            FontWeight = HeaderWeight(document)
        };

        void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DocumentViewModel.IsSelected))
            {
                title.FontWeight = HeaderWeight(document);
            }
        }

        document.PropertyChanged += OnDocumentPropertyChanged;

        _headerSubscriptions.Add((document, OnDocumentPropertyChanged));

        object header = title;

        if (document.Accent is { } accent)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };

            panel.Children.Add(new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Center,
                Background = accent
            });

            panel.Children.Add(title);

            header = panel;
        }

        var item = new TabViewItem
        {
            Header = header,
            IsClosable = document.CanClose,
            Tag = document,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        item.Content = new Grid();

        item.Tapped += (_, _) => Dock?.NotifyActivated(document);

        return item;
    }

    private static Windows.UI.Text.FontWeight HeaderWeight(DocumentViewModel document)
        => document.IsSelected ? FontWeights.SemiBold : FontWeights.Normal;

    private void ClearHeaderSubscriptions()
    {
        foreach (var (document, handler) in _headerSubscriptions)
        {
            document.PropertyChanged -= handler;
        }

        _headerSubscriptions.Clear();
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => BuildTabs();

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TabGroupNode.SelectedDocument) || Group is null || _syncing)
        {
            return;
        }

        if (Group.SelectedDocument is { } selected && _items.TryGetValue(selected, out var item))
        {
            _syncing = true;
            Tabs.SelectedItem = item;
            _syncing = false;

            ShowSelectedView();

            UpdateCommands();
        }
    }

    private void OnTabsLoaded(object sender, RoutedEventArgs e)
    {
        if (FindDescendant<ContentPresenter>(Tabs, "TabContentPresenter") is { } content)
        {
            content.ContentTransitions = [];
        }

        if (FindDescendant<ListView>(Tabs) is { } strip)
        {
            strip.ItemContainerTransitions = [];
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
        if (_syncing || Group is null)
        {
            return;
        }

        if (Tabs.SelectedItem is TabViewItem { Tag: DocumentViewModel document })
        {
            Group.SelectedDocument = document;

            ShowSelectedView();

            UpdateCommands();

            Dock?.NotifySelectionChanged();

            Dock?.NotifyActivated(document);
        }
    }

    private void OnTabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        if (((args.Item as TabViewItem) ?? args.Tab)?.Tag is not DocumentViewModel document)
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
        if (!AcceptsCurrentDrag)
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

        if (document is not null && Group is { } group && Dock is { } dock && dock.FindGroup(document) is not null)
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
        if (point.Y < (Tabs.ActualHeight > 0 ? Tabs.ActualHeight : TabStripReserve))
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
