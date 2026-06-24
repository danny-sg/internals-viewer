namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// Where a dragged tab will land when dropped over a tab group's content area.
/// <see cref="Center"/> moves the document into the target group; the edge zones split the group.
/// </summary>
public enum DropZone
{
    None,
    Center,
    Left,
    Right,
    Top,
    Bottom
}
