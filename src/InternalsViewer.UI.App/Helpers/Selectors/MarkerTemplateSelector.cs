using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class MarkerTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultTemplate { get; set; } = null!;

    public DataTemplate VirtualTemplate { get; set; } = null!;

    public DataTemplate PointerTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        var marker = item as Marker;

        var pointerTypes = new[]
        {
            Internals.Engine.Annotations.DataStructureItemType.DownPagePointer,
            Internals.Engine.Annotations.DataStructureItemType.Rid,
            Internals.Engine.Annotations.DataStructureItemType.HeaderPageAddress,
            Internals.Engine.Annotations.DataStructureItemType.NextPage,
            Internals.Engine.Annotations.DataStructureItemType.PreviousPage,
        };

        if (pointerTypes.Any(p => p == marker?.ItemType))
        {
            return PointerTemplate;
        }

        if(marker is { StartPosition: < 0, EndPosition: < 0 })
        {
            return VirtualTemplate;
        }

        return DefaultTemplate;
    }
}