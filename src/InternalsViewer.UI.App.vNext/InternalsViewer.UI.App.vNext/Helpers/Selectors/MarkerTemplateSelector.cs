using InternalsViewer.UI.App.vNext.Models;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace InternalsViewer.UI.App.vNext.Helpers.Selectors;

public class MarkerTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultTemplate { get; set; } = null!;

    public DataTemplate PointerTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        var marker = item as Marker;

        var pointerTypes = new[]
        {
            Internals.Engine.Annotations.DataStructureItemType.DownPagePointer,
            Internals.Engine.Annotations.DataStructureItemType.Rid
        };

        if (pointerTypes.Any(p => p == marker?.ItemType))
        {
            return PointerTemplate;
        }

        return DefaultTemplate;
    }
}