using System.Collections.Generic;
using System.ComponentModel;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReferencesPanelView : UserControl
{
    public MemoryReferencesPanelView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public PlayheadMemoryViewModel? ViewModel => DataContext as PlayheadMemoryViewModel;

    private MemoryReferencesViewModel? Attached { get; set; }

    private List<MemoryReferenceNode>? BuiltRoots { get; set; }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (IsLoaded)
        {
            Attach();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Attach();

        LayoutHelpers.EnableHorizontalScrolling(ReferenceTree);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Detach();

    private void Attach()
    {
        var references = ViewModel?.References;

        if (!ReferenceEquals(references, Attached))
        {
            Detach();

            Attached = references;

            if (references is not null)
            {
                references.PropertyChanged += OnReferencesPropertyChanged;
            }
        }

        Rebuild();
    }

    private void Detach()
    {
        if (Attached is { } references)
        {
            references.PropertyChanged -= OnReferencesPropertyChanged;
        }

        Attached = null;
    }

    private void OnReferencesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MemoryReferencesViewModel.Roots))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (Attached is not { } references || ReferenceEquals(references.Roots, BuiltRoots))
        {
            return;
        }

        BuiltRoots = references.Roots;

        var existing = new Dictionary<MemoryHistoryPointer, TreeViewNode>();

        foreach (var node in ReferenceTree.RootNodes)
        {
            if (node.Content is MemoryReferenceNode reference)
            {
                existing.TryAdd(reference.Pointer, node);
            }
        }

        ReferenceTree.RootNodes.Clear();

        foreach (var root in references.Roots)
        {
            ReferenceTree.RootNodes.Add(existing.Remove(root.Pointer, out var node) ? node : CreateTreeNode(root));
        }
    }

    private static TreeViewNode CreateTreeNode(MemoryReferenceNode reference)
        => new() { Content = reference, HasUnrealizedChildren = reference.CanExpand };

    private async void OnReferenceTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        var node = args.Node;

        if (Attached is not { } references || !node.HasUnrealizedChildren || node.Content is not MemoryReferenceNode reference)
        {
            return;
        }

        node.HasUnrealizedChildren = false;

        var children = await references.GetChildrenAsync(reference);

        if (!node.IsExpanded)
        {
            node.HasUnrealizedChildren = reference.CanExpand;

            return;
        }

        foreach (var child in children)
        {
            node.Children.Add(CreateTreeNode(child));
        }
    }

    private void OnReferenceTreeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (args.Node.Content is MemoryReferenceNode { CanExpand: true })
        {
            args.Node.Children.Clear();

            args.Node.HasUnrealizedChildren = true;
        }
    }

    private void OnReferenceTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode { Content: MemoryReferenceNode reference })
        {
            Attached?.Select(reference);
        }
    }

    private void OnGraphClick(object sender, RoutedEventArgs e) => Attached?.OpenGraph();

    private void OnTargetClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TreeViewNode { Content: MemoryReferenceNode reference })
        {
            Attached?.Open(reference);
        }
    }
}
