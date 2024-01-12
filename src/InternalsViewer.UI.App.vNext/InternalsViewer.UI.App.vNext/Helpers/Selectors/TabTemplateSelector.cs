using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.Helpers.Selectors;

public class TabTemplateSelector : DataTemplateSelector
{
    public DataTemplate PageTemplate { get; set; } = null!;

    public DataTemplate DatabaseTemplate { get; set; } = null!;

    public DataTemplate GetStartedTemplate { get; set; } = null!;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
       var tab = item as TabViewModel;

        return tab?.TabType switch
        {
            TabType.Page => PageTemplate,
            TabType.Database => DatabaseTemplate,
            TabType.Connect => GetStartedTemplate,
            _ => null
        };
    }
}
