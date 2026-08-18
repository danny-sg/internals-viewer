using System.Linq;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Markers;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public class MarkerTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// Item types that point at another page or row without holding a page address value
    /// </summary>
    private static readonly ItemType[] PointerTypes =
    [
        ItemType.DownPagePointer,
        ItemType.Rid,
        ItemType.HeaderPageAddress,
        ItemType.NextPage,
        ItemType.PreviousPage,
        ItemType.ForwardingStub
    ];

    public DataTemplate DefaultTemplate { get; set; } = null!;

    public DataTemplate PointerTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is not Marker marker)
        {
            return DefaultTemplate;
        }

        if (marker.MarkerType == MarkerType.PageAddress || PointerTypes.Any(p => p == marker.Type))
        {
            return PointerTemplate;
        }

        return DefaultTemplate;
    }
}
