using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One page of the delta store as the page list shows it
/// </summary>
public sealed record DeltaStorePageSummary(PageAddress PageAddress, int SlotCount, int FreeBytes, string PageType)
{
    public string PageAddressDescription => PageAddress.ToString();
}
