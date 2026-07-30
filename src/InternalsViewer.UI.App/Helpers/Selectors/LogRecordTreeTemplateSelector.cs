using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class LogRecordTreeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecordTemplate { get; set; }

    public DataTemplate? AnnotationTemplate { get; set; }

    public DataTemplate? AnnotationHeaderTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return item switch
        {
            TreeViewNode { Content: AnnotationHeader } => AnnotationHeaderTemplate,
            TreeViewNode { Content: LogRecordAnnotation } => AnnotationTemplate,
            _ => RecordTemplate
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
