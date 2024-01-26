using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

internal class SlotTemplateSelector : DataTemplateSelector
{
    public DataTemplate SlotTemplate { get; set; } = null!;

    public DataTemplate ItemTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        var slot = item as OffsetSlot;

        if (slot?.Index < 0)
        {
            return ItemTemplate;
        }

        return SlotTemplate;
    }
}
