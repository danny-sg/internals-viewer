using System;
using System.Collections.Generic;
using System.ComponentModel;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryHashTableView : UserControl, IDisposable
{
    public MemoryHashTableView()
    {
        InitializeComponent();

        HexColumn.Width = new GridLength(HexViewControl.StandardWidth);

        DataContextChanged += OnDataContextChanged;

        Loaded += (_, _) => LayoutHelpers.EnableHorizontalScrolling(StructureTree);
    }

    public MemoryHashTableViewModel? ViewModel => DataContext as MemoryHashTableViewModel;

    private MemoryHashTableViewModel? Attached { get; set; }

    private Dictionary<string, bool> Expansion { get; } = [];

    private bool IsSyncing { get; set; }

    public void Dispose() => Attach(null);

    private static bool IsExpandedByDefault(MemoryHashTableNode node)
        => node.Type is MemoryHashTableNodeType.Group or MemoryHashTableNodeType.Partitions;

    private static TreeViewNode? Find(IList<TreeViewNode> nodes, string key)
    {
        foreach (var node in nodes)
        {
            if (node.Content is MemoryHashTableNode { Key: var nodeKey } && nodeKey == key)
            {
                return node;
            }

            if (Find(node.Children, key) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        Attach(ViewModel);
    }

    private void Attach(MemoryHashTableViewModel? viewModel)
    {
        if (ReferenceEquals(viewModel, Attached))
        {
            return;
        }

        if (Attached is { } previous)
        {
            previous.PropertyChanged -= OnViewModelPropertyChanged;
        }

        Attached = viewModel;

        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        Rebuild();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MemoryHashTableViewModel.Roots):
                Rebuild();
                break;

            case nameof(MemoryHashTableViewModel.SelectedNode):
                SyncSelection();
                break;
        }
    }

    private void Rebuild()
    {
        CaptureExpansion(StructureTree.RootNodes);

        IsSyncing = true;

        StructureTree.RootNodes.Clear();

        foreach (var root in Attached?.Roots ?? [])
        {
            StructureTree.RootNodes.Add(CreateNode(root));
        }

        IsSyncing = false;

        SyncSelection();
    }

    private void CaptureExpansion(IList<TreeViewNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Content is MemoryHashTableNode content && node.Children.Count > 0)
            {
                Expansion[content.Key] = node.IsExpanded;
            }

            CaptureExpansion(node.Children);
        }
    }

    private TreeViewNode CreateNode(MemoryHashTableNode node)
    {
        var tree = new TreeViewNode { Content = node };

        foreach (var child in node.Children)
        {
            tree.Children.Add(CreateNode(child));
        }

        tree.IsExpanded = Expansion.TryGetValue(node.Key, out var isExpanded) ? isExpanded : IsExpandedByDefault(node);

        return tree;
    }

    private void SyncSelection()
    {
        if (Attached?.SelectedNode is not { } selected
            || Find(StructureTree.RootNodes, selected.Key) is not { } node
            || ReferenceEquals(StructureTree.SelectedNode, node))
        {
            return;
        }

        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            parent.IsExpanded = true;
        }

        IsSyncing = true;

        StructureTree.SelectedNode = node;

        IsSyncing = false;
    }

    private void OnStructureSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (IsSyncing || Attached is not { } viewModel || sender.SelectedNode?.Content is not MemoryHashTableNode node)
        {
            return;
        }

        if (node.IsSelectable)
        {
            viewModel.SelectedNode = node;

            return;
        }

        DispatcherQueue.TryEnqueue(SyncSelection);
    }

    private void OnStructureItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode { Content: MemoryHashTableNode { IsSelectable: false } } node)
        {
            node.IsExpanded = !node.IsExpanded;
        }
    }

    private void OnHexOffsetClicked(object? sender, int offset) => ViewModel?.SelectOffset(offset);

    private void OnPathClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ActionLink link })
        {
            link.Invoke();
        }
    }
}
