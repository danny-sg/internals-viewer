using InternalsViewer.Query.Events.Operators;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>
/// Picks the row template for the Callstack tree — a plan operator (in Plan Operators mode) or a call frame
/// </summary>
public sealed class CallstackNodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? OperatorTemplate { get; set; }

    public DataTemplate? FrameTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) =>
        item is TreeViewNode { Content: ExecutionOperatorEvent } && OperatorTemplate is not null
            ? OperatorTemplate
            : FrameTemplate!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}
