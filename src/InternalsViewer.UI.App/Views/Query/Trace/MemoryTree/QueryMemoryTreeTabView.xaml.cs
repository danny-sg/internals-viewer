using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.MemoryTree;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryTree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Trace.MemoryTree;

public sealed partial class QueryMemoryTreeTabView : UserControl, IDisposable
{
    private const int ExpandAllLimit = 500;

    private const string HashTableTag = "HashTable";

    public QueryMemoryTreeTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        Loaded += OnLoaded;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private MemoryTreeViewModel? Attached { get; set; }

    private HashSet<string> Expanded { get; } = [];

    private TreeViewNode? ContextNode { get; set; }

    public void Dispose() => Attach(null);

    private static TreeViewNode CreateNode(MemoryTreeNode node) => new() { Content = node, HasUnrealizedChildren = node.HasChildren };

    private static void Realize(MemoryTreeViewModel tree, TreeViewNode node, MemoryTreeNode content)
    {
        node.HasUnrealizedChildren = false;

        foreach (var child in tree.GetChildren(content))
        {
            node.Children.Add(CreateNode(child));
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        Attach(ViewModel?.MemoryTree);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LayoutHelpers.EnableHorizontalScrolling(ObjectTree);

        Attach(ViewModel?.MemoryTree);

        Attached?.Show();
    }

    private void Attach(MemoryTreeViewModel? tree)
    {
        if (ReferenceEquals(tree, Attached))
        {
            return;
        }

        if (Attached is { } previous)
        {
            previous.PropertyChanged -= OnTreePropertyChanged;
        }

        Attached = tree;

        if (tree is not null)
        {
            tree.PropertyChanged += OnTreePropertyChanged;
        }

        Rebuild();
    }

    private void OnTreePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MemoryTreeViewModel.Roots))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        ObjectTree.RootNodes.Clear();

        if (Attached is not { } tree)
        {
            return;
        }

        var isExpandingAll = tree.IsFiltered && tree.KeptCount <= ExpandAllLimit;

        var pending = new Queue<TreeViewNode>();

        foreach (var root in tree.Roots)
        {
            var node = CreateNode(root);

            ObjectTree.RootNodes.Add(node);

            pending.Enqueue(node);
        }

        while (pending.TryDequeue(out var node))
        {
            if (node.Content is not MemoryTreeNode { HasChildren: true } content || !(isExpandingAll || Expanded.Contains(content.Key)))
            {
                continue;
            }

            Realize(tree, node, content);

            node.IsExpanded = true;

            foreach (var child in node.Children)
            {
                pending.Enqueue(child);
            }
        }
    }

    private void OnTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Node.Content is not MemoryTreeNode content || Attached is not { } tree)
        {
            return;
        }

        if (!tree.IsFiltered)
        {
            Expanded.Add(content.Key);
        }

        if (args.Node.HasUnrealizedChildren)
        {
            Realize(tree, args.Node, content);
        }
    }

    private void OnTreeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (args.Node.Content is MemoryTreeNode content && Attached is { IsFiltered: false })
        {
            Expanded.Remove(content.Key);
        }
    }

    private void OnTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is not TreeViewNode { Content: MemoryTreeNode content } node)
        {
            return;
        }

        if (content.IsObject)
        {
            Attached?.Open(content);
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

        var node = ContextNode?.Content as MemoryTreeNode;

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
        if (ContextNode?.Content is MemoryTreeNode node)
        {
            Attached?.Open(node);
        }
    }

    private void OnHashTableClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is MemoryTreeNode node)
        {
            Attached?.OpenHashTable(node);
        }
    }

    private void OnObjectGraphClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is MemoryTreeNode node)
        {
            Attached?.OpenGraph(node);
        }
    }
}
