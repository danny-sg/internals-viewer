using InternalsViewer.UI.App.Models.Query.Trace.Calls.Stack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public sealed class PlayheadStackStyleSelector : StyleSelector
{
    public Style ThreadStyle { get; set; } = null!;

    public Style CallStyle { get; set; } = null!;

    protected override Style SelectStyleCore(object item, DependencyObject container)
        => item is TreeViewNode { Content: PlayheadThreadRow } ? ThreadStyle : CallStyle;
}
