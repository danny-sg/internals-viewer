using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class AccessStepTemplateSelector : DataTemplateSelector
{
    public DataTemplate ReadPageTemplate { get; set; } = null!;

    public DataTemplate ProbeStartTemplate { get; set; } = null!;

    public DataTemplate ProbeTemplate { get; set; } = null!;

    public DataTemplate DescendTemplate { get; set; } = null!;

    public DataTemplate ProbeResultTemplate { get; set; } = null!;

    public DataTemplate RowTemplate { get; set; } = null!;

    public DataTemplate RangeEndTemplate { get; set; } = null!;

    public DataTemplate LeafLinkTemplate { get; set; } = null!;

    public DataTemplate ReseekTemplate { get; set; } = null!;

    public DataTemplate StoppedTemplate { get; set; } = null!;

    public DataTemplate TruncatedTemplate { get; set; } = null!;

    public DataTemplate DefaultTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            AccessStep.ReadPage => ReadPageTemplate,
            AccessStep.ProbeStart => ProbeStartTemplate,
            AccessStep.Probe => ProbeTemplate,
            AccessStep.Descend => DescendTemplate,
            AccessStep.ProbeResult => ProbeResultTemplate,
            AccessStep.Row => RowTemplate,
            AccessStep.RangeEnd => RangeEndTemplate,
            AccessStep.LeafLink => LeafLinkTemplate,
            AccessStep.Reseek => ReseekTemplate,
            AccessStep.Stopped => StoppedTemplate,
            AccessStep.Truncated => TruncatedTemplate,
            _ => DefaultTemplate
        };
    }
}
