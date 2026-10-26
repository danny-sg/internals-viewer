using System.Linq;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

internal static class VectorTypeMenu
{
    private const string NoType = "None";

    public static void Add(MenuFlyout menu, MemoryBatchViewModel batch, int ordinal, bool isVectorsTable)
    {
        if (batch.GetVectorId(ordinal, isVectorsTable) is not { } vectorId)
        {
            return;
        }

        var current = batch.GetVectorType(vectorId);

        menu.Items.Add(MenuItems.Toggle(NoType, current is null, _ => batch.SetVectorType(vectorId, null)));

        foreach (var type in BatchSlotFormatter.Types)
        {
            menu.Items.Add(MenuItems.Toggle(type.ToString(), current == type, _ => batch.SetVectorType(vectorId, type)));
        }
    }
}
