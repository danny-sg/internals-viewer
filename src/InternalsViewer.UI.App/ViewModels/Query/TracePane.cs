namespace InternalsViewer.UI.App.ViewModels.Query;

public enum TracePaneKind
{
    Empty,
    Visual,
    OperatorResults,
    Records,
    HashTable
}

/// <summary>
/// One pane of an operator's tab, which is what it shows and what that view binds to
/// </summary>
/// <remarks>
/// The definition tree decides the kind, so an input that is another operator resolves to that operator's results rather than to a visual.
/// The panes are laid out by one view rather than by the dock, because an operator is a single document and its panes are not moveable.
/// </remarks>
public sealed record TracePane(TracePaneKind Kind, object? Content, string Title = "")
{
    public static readonly TracePane Empty = new(TracePaneKind.Empty, null);
}
