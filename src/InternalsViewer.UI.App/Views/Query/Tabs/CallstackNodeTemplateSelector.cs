using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>
/// Picks the row template for the Callstack tree — a plan operator (when scoped) or a call frame
/// </summary>
/// <remarks>
/// Also drives the scope header, whose rows are the same things drawn the same way. Every item is a TreeViewNode
/// because that is what a TreeView hands its template selector, so the header wraps its content in one to match.
/// </remarks>
public sealed class CallstackNodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? OperatorTemplate { get; set; }

    public DataTemplate? LinkTemplate { get; set; }

    public DataTemplate? EventTemplate { get; set; }

    public DataTemplate? FrameTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) => item switch
    {
        TreeViewNode { Content: OperatorRow } when OperatorTemplate is not null => OperatorTemplate,
        TreeViewNode { Content: OperatorLink } when LinkTemplate is not null => LinkTemplate,
        TreeViewNode { Content: EventRow } when EventTemplate is not null => EventTemplate,
        _ => FrameTemplate!,
    };

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
