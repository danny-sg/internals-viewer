using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class MarkerTreeView
{
    public event EventHandler<PageAddressEventArgs>? PageClicked;

    private readonly Dictionary<Marker, TreeViewNode> _nodesByMarker = new(ReferenceEqualityComparer.Instance);

    // SelectedMarker is TwoWay-bound to the same ViewModel property from both this control and
    // HexViewControl, so a selection made here can echo back through the view model. Guards against
    // that turning into an unbounded TreeView_SelectionChanged <-> OnSelectedMarkerChanged cycle.
    private bool _isSyncingSelection;

    public ObservableCollection<Marker>? Markers
    {
        get => (ObservableCollection<Marker>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    public static readonly DependencyProperty MarkersProperty = DependencyProperty
        .Register(nameof(Markers),
            typeof(ObservableCollection<Marker>),
            typeof(MarkerTreeView),
            new PropertyMetadata(null, OnMarkersChanged));

    // TreeView.ItemsSource has a known WinUI bug (microsoft/microsoft-ui-xaml#7044) where it reuses
    // TreeViewNode/TreeViewItem containers without resetting their state, which is especially bad
    // here since the nested Children are also driven off ItemsSource - stale child nodes can be left
    // on screen after a new Markers value comes in. Building TreeViewNodes directly and clearing
    // RootNodes on every change avoids the recycling path entirely, matching the workaround Microsoft
    // recommends for that issue.
    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MarkerTreeView control)
        {
            return;
        }

        var markers = (e.NewValue as ObservableCollection<Marker>)?.Where(m => m.IsVisible) ?? [];

        control.TreeView.RootNodes.Clear();
        control._nodesByMarker.Clear();

        foreach (var marker in markers)
        {
            control.TreeView.RootNodes.Add(control.BuildNode(marker));
        }
    }

    private TreeViewNode BuildNode(Marker marker)
    {
        var node = new TreeViewNode { Content = marker, IsExpanded = true };

        _nodesByMarker[marker] = node;

        foreach (var child in marker.Children)
        {
            node.Children.Add(BuildNode(child));
        }

        return node;
    }

    public Marker? SelectedMarker
    {
        get => (Marker?)GetValue(SelectedMarkerProperty);
        set => SetValue(SelectedMarkerProperty, value);
    }

    public static readonly DependencyProperty SelectedMarkerProperty
        = DependencyProperty.Register(nameof(SelectedMarker),
            typeof(Marker),
            typeof(MarkerTreeView),
            new PropertyMetadata(null, OnSelectedMarkerChanged));

    // TreeView.SelectedItem only round-trips the underlying data item when bound via ItemsSource;
    // since Markers is now driven off RootNodes (see OnMarkersChanged), selection has to be
    // synced manually between our own Marker-typed SelectedMarker and the TreeView's TreeViewNode.
    // Looked up via the _nodesByMarker dictionary rather than walking the node tree, since a
    // recursive search here re-runs on every echoed selection change (see _isSyncingSelection).
    private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MarkerTreeView control || control._isSyncingSelection)
        {
            return;
        }

        control._isSyncingSelection = true;

        control.TreeView.SelectedNode = e.NewValue is Marker marker
                                         && control._nodesByMarker.TryGetValue(marker, out var node)
            ? node
            : null;

        control._isSyncingSelection = false;
    }

    private void TreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;

        SelectedMarker = sender.SelectedNode?.Content as Marker;

        _isSyncingSelection = false;
    }

    public MarkerTreeView()
    {
        InitializeComponent();
    }

    private void PageLink_Click(object sender, RoutedEventArgs e)
    {
        var value = ((HyperlinkButton)sender).Content.ToString();

        if (value != null)
        {
            var rowIdentifier = RowIdentifier.Parse(value);

            var eventArgs = new PageAddressEventArgs(rowIdentifier.PageAddress.FileId, rowIdentifier.PageAddress.PageId)
            {
                Slot = rowIdentifier.SlotId
            };

            PageClicked?.Invoke(this, eventArgs);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var value = (sender as CopyButton)?.Tag.ToString() ?? string.Empty;

        var package = new DataPackage();

        package.SetText(value);

        Clipboard.SetContent(package);
    }
}
