using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class MarkerTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultTemplate { get; set; } = null!;

    public DataTemplate PointerTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        var marker = item as Marker;

        var pointerTypes = new[]
        {
            ItemType.DownPagePointer,
            ItemType.Rid,
            ItemType.HeaderPageAddress,
            ItemType.NextPage,
            ItemType.PreviousPage,
        };

        if (pointerTypes.Any(p => p == marker?.Type))
        {
            return PointerTemplate;
        }

        return DefaultTemplate;
    }
}