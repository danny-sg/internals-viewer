using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// An internal node splitting its area between two children. <see cref="Orientation"/>
/// is <see cref="Orientation.Horizontal"/> for a left/right split and
/// <see cref="Orientation.Vertical"/> for a top/bottom split.
/// </summary>
public sealed partial class SplitNode : LayoutNode
{
    public SplitNode(Orientation orientation, LayoutNode first, LayoutNode second)
    {
        Orientation = orientation;
        First = first;
        Second = second;

        first.Parent = this;
        second.Parent = this;
    }

    [ObservableProperty]
    private Orientation orientation;

    [ObservableProperty]
    private LayoutNode first;

    [ObservableProperty]
    private LayoutNode second;

    /// <summary>Star weight of the first child; paired with <see cref="SecondStar"/> to size the two panes.</summary>
    [ObservableProperty]
    private double firstStar = 1;

    [ObservableProperty]
    private double secondStar = 1;

    partial void OnFirstChanged(LayoutNode value) => value.Parent = this;

    partial void OnSecondChanged(LayoutNode value) => value.Parent = this;

    /// <summary>Returns the child that is not <paramref name="node"/>, or <c>null</c> if it isn't a child.</summary>
    public LayoutNode? Sibling(LayoutNode node)
    {
        if (ReferenceEquals(First, node))
        {
            return Second;
        }

        return ReferenceEquals(Second, node) ? First : null;
    }

    public void Replace(LayoutNode existing, LayoutNode replacement)
    {
        if (ReferenceEquals(First, existing))
        {
            First = replacement;
        }
        else if (ReferenceEquals(Second, existing))
        {
            Second = replacement;
        }
    }
}
