using System;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// Converts a dock layout tree to/from its serializable form
/// </summary>
public static class DockLayoutSerializer
{
    public static DockNode Serialize(LayoutNode node)
    {
        if (node is FixedNode)
        {
            return new DockNode { IsFixed = true };
        }

        if (node is TabGroupNode group)
        {
            var persisted = group.Documents.Where(d => d.Persist).Select(d => d.Key).ToList();

            return new DockNode
            {
                IsSplit = false,
                Documents = persisted,
                Selected = group.SelectedDocument is { Persist: true } selected ? selected.Key : persisted.FirstOrDefault()
            };
        }

        var split = (SplitNode)node;

        return new DockNode
        {
            IsSplit = true,
            Orientation = (int)split.Orientation,
            FirstStar = split.FirstStar,
            SecondStar = split.SecondStar,
            First = Serialize(split.First),
            Second = Serialize(split.Second)
        };
    }

    /// <summary>
    /// Rebuilds a layout tree from <paramref name="node"/>, resolving documents via <paramref name="resolve"/>
    /// </summary>
    public static LayoutNode? Deserialize(DockNode? node, Func<string, DocumentViewModel?> resolve)
    {
        if (node is null)
        {
            return null;
        }

        if (node.IsFixed)
        {
            return new FixedNode();
        }

        if (!node.IsSplit)
        {
            var documents = node.Documents.Select(resolve).OfType<DocumentViewModel>().Distinct().ToArray();

            if (documents.Length == 0)
            {
                return null;
            }

            return new TabGroupNode(documents)
            {
                SelectedDocument = documents.FirstOrDefault(d => d.Key == node.Selected) ?? documents[0]
            };
        }

        var first = Deserialize(node.First, resolve);
        var second = Deserialize(node.Second, resolve);

        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return new SplitNode((Orientation)node.Orientation, first, second)
        {
            FirstStar = node.FirstStar,
            SecondStar = node.SecondStar
        };
    }
}
