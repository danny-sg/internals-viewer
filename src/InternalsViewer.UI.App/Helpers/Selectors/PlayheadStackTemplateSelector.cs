using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public sealed class PlayheadStackTemplateSelector : DataTemplateSelector
{
    public DataTemplate ThreadTemplate { get; set; } = null!;

    public DataTemplate CallTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item)
        => item is TreeViewNode { Content: PlayheadThreadRow } ? ThreadTemplate : CallTemplate;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
