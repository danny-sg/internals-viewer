using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>
/// Picks the row template for the Callstack tree — a plan operator (when scoped) or a call frame
/// </summary>
public sealed class CallstackNodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? OperatorTemplate { get; set; }

    public DataTemplate? LinkTemplate { get; set; }

    public DataTemplate? FrameTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) => item switch
    {
        TreeViewNode { Content: OperatorRow } when OperatorTemplate is not null => OperatorTemplate,
        TreeViewNode { Content: OperatorLink } when LinkTemplate is not null => LinkTemplate,
        _ => FrameTemplate!,
    };

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
