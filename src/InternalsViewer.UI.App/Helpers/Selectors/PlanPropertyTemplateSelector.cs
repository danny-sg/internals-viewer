using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class PlanPropertyTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultTemplate { get; set; } = null!;

    public DataTemplate PredicateTemplate { get; set; } = null!;

    public DataTemplate ListItemTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            TreeViewNode { Content: PlanNodeProperty { Predicate: not null } } => PredicateTemplate,
            TreeViewNode { Content: PlanNodeProperty { Items.Count: > 0 } } => ListItemTemplate,
            _ => DefaultTemplate
        };
    }
}
