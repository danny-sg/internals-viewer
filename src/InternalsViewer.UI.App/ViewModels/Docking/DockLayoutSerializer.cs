using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>Serialisable snapshot of a dock layout node (group or split), referencing documents by key.</summary>
public sealed class DockNodeDto
{
    public bool IsSplit { get; set; }

    // Group
    public List<string> Documents { get; set; } = [];

    public string? Selected { get; set; }

    // Split
    public int Orientation { get; set; }

    public double FirstStar { get; set; } = 1;

    public double SecondStar { get; set; } = 1;

    public DockNodeDto? First { get; set; }

    public DockNodeDto? Second { get; set; }
}

/// <summary>Converts a dock layout tree to/from its serialisable form, mapping documents by key.</summary>
public static class DockLayoutSerializer
{
    public static DockNodeDto Serialize(LayoutNode node)
    {
        if (node is TabGroupNode group)
        {
            return new DockNodeDto
            {
                IsSplit = false,
                Documents = group.Documents.Select(d => d.Key).ToList(),
                Selected = group.SelectedDocument?.Key
            };
        }

        var split = (SplitNode)node;

        return new DockNodeDto
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
    /// Rebuilds a layout tree from <paramref name="dto"/>, resolving documents via <paramref name="resolve"/>.
    /// Unknown documents are dropped and groups/splits that end up empty collapse away (returning null).
    /// </summary>
    public static LayoutNode? Deserialize(DockNodeDto? dto, Func<string, DocumentViewModel?> resolve)
    {
        if (dto is null)
        {
            return null;
        }

        if (!dto.IsSplit)
        {
            var group = new TabGroupNode();

            foreach (var key in dto.Documents)
            {
                if (resolve(key) is { } document && !group.Documents.Contains(document))
                {
                    group.Documents.Add(document);
                }
            }

            if (group.Documents.Count == 0)
            {
                return null;
            }

            group.SelectedDocument = (dto.Selected is not null ? resolve(dto.Selected) : null)
                                     ?? group.Documents.FirstOrDefault();

            return group;
        }

        var first = Deserialize(dto.First, resolve);
        var second = Deserialize(dto.Second, resolve);

        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return new SplitNode((Orientation)dto.Orientation, first, second)
        {
            FirstStar = dto.FirstStar,
            SecondStar = dto.SecondStar
        };
    }
}
