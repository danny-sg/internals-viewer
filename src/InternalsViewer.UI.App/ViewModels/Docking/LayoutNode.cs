using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// Base for the recursive dock layout tree. A node is either a <see cref="TabGroupNode"/>
/// (a leaf holding documents) or a <see cref="SplitNode"/> (two children separated by a splitter).
/// </summary>
public abstract partial class LayoutNode : ObservableObject
{
    /// <summary>The split that contains this node, or <c>null</c> when this node is the tree root.</summary>
    [ObservableProperty]
    private SplitNode? parent;
}
