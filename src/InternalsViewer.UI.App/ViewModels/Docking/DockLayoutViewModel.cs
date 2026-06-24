using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// Owns the recursive dock layout tree and the structural operations that mutate it
/// (moving a document, splitting a group, closing a document, collapsing empty groups).
/// Raises <see cref="LayoutChanged"/> after any structural change so the view can rebuild.
/// </summary>
public sealed partial class DockLayoutViewModel : ObservableObject
{
    [ObservableProperty]
    private LayoutNode root;

    /// <summary>Raised after the tree's shape changes (split, move between groups, collapse).</summary>
    public event EventHandler? LayoutChanged;

    public DockLayoutViewModel(LayoutNode root)
    {
        this.root = root;
    }

    private void OnLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>All tab groups in the tree, depth first.</summary>
    public IEnumerable<TabGroupNode> Groups() => Groups(Root);

    private static IEnumerable<TabGroupNode> Groups(LayoutNode node)
    {
        switch (node)
        {
            case TabGroupNode group:
                yield return group;
                break;
            case SplitNode split:
                foreach (var g in Groups(split.First))
                {
                    yield return g;
                }

                foreach (var g in Groups(split.Second))
                {
                    yield return g;
                }

                break;
        }
    }

    public TabGroupNode? FindGroup(DocumentViewModel document)
        => Groups().FirstOrDefault(g => g.Documents.Contains(document));

    /// <summary>Selects <paramref name="document"/> in whichever group currently hosts it.</summary>
    public void Activate(DocumentViewModel document)
    {
        var group = FindGroup(document);

        if (group is not null)
        {
            group.SelectedDocument = document;
        }
    }

    /// <summary>
    /// Moves <paramref name="document"/> onto <paramref name="target"/>. A <see cref="DropZone.Center"/>
    /// drop appends it to the target group; an edge zone splits the target and places the document
    /// in a new group on that side.
    /// </summary>
    public void Move(DocumentViewModel document, TabGroupNode target, DropZone zone)
    {
        if (zone is DropZone.None)
        {
            return;
        }

        var source = FindGroup(document);

        if (source is null)
        {
            return;
        }

        // Dragging the only tab of a group onto its own edge would split a group against itself.
        if (zone is not DropZone.Center && ReferenceEquals(source, target) && source.Documents.Count == 1)
        {
            return;
        }

        if (zone is DropZone.Center)
        {
            if (ReferenceEquals(source, target))
            {
                target.SelectedDocument = document;
                return;
            }

            MoveToGroup(document, source, target);
            CollapseIfEmpty(source);
            OnLayoutChanged();
            return;
        }

        SplitInto(document, source, target, zone);
    }

    private void SplitInto(DocumentViewModel document, TabGroupNode source, TabGroupNode target, DropZone zone)
    {
        source.Documents.Remove(document);

        if (ReferenceEquals(source.SelectedDocument, document))
        {
            source.SelectedDocument = source.Documents.FirstOrDefault();
        }

        var newGroup = new TabGroupNode(document);

        var orientation = zone is DropZone.Left or DropZone.Right
            ? Orientation.Horizontal
            : Orientation.Vertical;

        var newGroupFirst = zone is DropZone.Left or DropZone.Top;

        var parent = target.Parent;

        var split = newGroupFirst
            ? new SplitNode(orientation, newGroup, target)
            : new SplitNode(orientation, target, newGroup);

        if (parent is null)
        {
            Root = split;
        }
        else
        {
            parent.Replace(target, split);
            split.Parent = parent;
        }

        // Source may now be empty (only when it differed from target).
        if (!ReferenceEquals(source, target))
        {
            CollapseIfEmpty(source);
        }

        OnLayoutChanged();
    }

    private static void MoveToGroup(DocumentViewModel document, TabGroupNode source, TabGroupNode target)
    {
        source.Documents.Remove(document);

        if (ReferenceEquals(source.SelectedDocument, document))
        {
            source.SelectedDocument = source.Documents.FirstOrDefault();
        }

        target.Documents.Add(document);
        target.SelectedDocument = document;
    }

    /// <summary>Removes a document from the layout, collapsing its group if it becomes empty.</summary>
    public void Close(DocumentViewModel document)
    {
        var group = FindGroup(document);

        if (group is null)
        {
            return;
        }

        group.Documents.Remove(document);

        if (ReferenceEquals(group.SelectedDocument, document))
        {
            group.SelectedDocument = group.Documents.FirstOrDefault();
        }

        CollapseIfEmpty(group);
        OnLayoutChanged();
    }

    private void CollapseIfEmpty(TabGroupNode group)
    {
        if (group.Documents.Count > 0)
        {
            return;
        }

        var parent = group.Parent;

        // Last remaining group: keep it (empty) as the root rather than leaving no layout.
        if (parent is null)
        {
            return;
        }

        var sibling = parent.Sibling(group);

        if (sibling is null)
        {
            return;
        }

        var grandparent = parent.Parent;

        if (grandparent is null)
        {
            Root = sibling;
            sibling.Parent = null;
        }
        else
        {
            grandparent.Replace(parent, sibling);
            sibling.Parent = grandparent;
        }
    }
}
