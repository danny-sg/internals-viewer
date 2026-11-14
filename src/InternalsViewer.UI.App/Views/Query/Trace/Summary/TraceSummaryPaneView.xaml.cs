using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Summary;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Trace.Summary;

public sealed partial class TraceSummaryPaneView : UserControl, IDisposable
{
    private const string HashTableTag = "HashTable";

    private const string MemoryToolbarKey = "MemoryToolbarTemplate";

    public TraceSummaryPaneView(TraceSummaryPaneViewModel pane)
    {
        Pane = pane;

        InitializeComponent();

        if (pane is TraceSummaryMemoryViewModel)
        {
            Toolbar.ContentTemplate = (DataTemplate)Resources[MemoryToolbarKey];

            Toolbar.Content = pane;
        }
        else
        {
            Toolbar.Visibility = Visibility.Collapsed;
        }

        pane.PropertyChanged += OnPanePropertyChanged;

        Loaded += OnLoaded;

        Rebuild();
    }

    public TraceSummaryPaneViewModel Pane { get; }

    private HashSet<string> Expanded { get; } = [];

    private TreeViewNode? ContextNode { get; set; }

    public static SqlDbType? GetDataType(TraceSummaryNode node)
        => node.ColumnType is { } type && SqlTypeHelpers.TryGetSqlType(type.TypeId, out var dataType) ? dataType : null;

    public static int GetPrecision(TraceSummaryNode node) => node.ColumnType?.Precision ?? 0;

    public static int GetScale(TraceSummaryNode node) => node.ColumnType?.Scale ?? 0;

    public static int GetLength(TraceSummaryNode node) => node.ColumnType?.MaxLength ?? 0;

    public void Dispose() => Pane.PropertyChanged -= OnPanePropertyChanged;

    private static TreeViewNode CreateNode(TraceSummaryNode node) => new() { Content = node, HasUnrealizedChildren = node.HasChildren };

    private static void Realize(TraceSummaryPaneViewModel pane, TreeViewNode node, TraceSummaryNode content)
    {
        node.HasUnrealizedChildren = false;

        foreach (var child in pane.GetChildren(content))
        {
            node.Children.Add(CreateNode(child));
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => LayoutHelpers.EnableHorizontalScrolling(ObjectTree);

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TraceSummaryPaneViewModel.Roots))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        ObjectTree.RootNodes.Clear();

        var isExpandingAll = Pane.IsExpandingAll;

        var pending = new Queue<TreeViewNode>();

        foreach (var root in Pane.Roots)
        {
            var node = CreateNode(root);

            ObjectTree.RootNodes.Add(node);

            pending.Enqueue(node);
        }

        while (pending.TryDequeue(out var node))
        {
            if (node.Content is not TraceSummaryNode { HasChildren: true } content || !(isExpandingAll || Expanded.Contains(content.Key)))
            {
                continue;
            }

            Realize(Pane, node, content);

            node.IsExpanded = true;

            foreach (var child in node.Children)
            {
                pending.Enqueue(child);
            }
        }
    }

    private void OnTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Node.Content is not TraceSummaryNode content)
        {
            return;
        }

        if (!Pane.IsFiltered)
        {
            Expanded.Add(content.Key);
        }

        if (args.Node.HasUnrealizedChildren)
        {
            Realize(Pane, args.Node, content);
        }
    }

    private void OnTreeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (args.Node.Content is TraceSummaryNode content && !Pane.IsFiltered)
        {
            Expanded.Remove(content.Key);
        }
    }

    private void OnTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is not TreeViewNode { Content: TraceSummaryNode content } node)
        {
            return;
        }

        if (content.IsObject)
        {
            Pane.Open(content);
        }
        else
        {
            node.IsExpanded = !node.IsExpanded;
        }
    }

    private void OnNodeRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        ContextNode = (sender as FrameworkElement)?.DataContext as TreeViewNode;

        if ((sender as FrameworkElement)?.ContextFlyout is not MenuFlyout flyout)
        {
            return;
        }

        var node = ContextNode?.Content as TraceSummaryNode;

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            if (item.Tag is HashTableTag)
            {
                item.Visibility = node is { HashTableGroup: not 0 } ? Visibility.Visible : Visibility.Collapsed;

                continue;
            }

            item.IsEnabled = node is { IsObject: true };
        }
    }

    private void OnOpenMemoryClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is TraceSummaryNode node)
        {
            Pane.Open(node);
        }
    }

    private void OnHashTableClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is TraceSummaryNode node)
        {
            Pane.OpenHashTable(node);
        }
    }

    private void OnObjectGraphClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is TraceSummaryNode node)
        {
            Pane.OpenGraph(node);
        }
    }
}
