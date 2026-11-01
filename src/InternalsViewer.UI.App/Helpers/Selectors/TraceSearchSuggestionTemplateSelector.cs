using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public sealed class TraceSearchSuggestionTemplateSelector : DataTemplateSelector
{
    public DataTemplate CallTemplate { get; set; } = null!;

    public DataTemplate MemoryTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item) => item is MemorySearchSuggestion ? MemoryTemplate : CallTemplate;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
