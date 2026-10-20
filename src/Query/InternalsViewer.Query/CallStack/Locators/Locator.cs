using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct Locator(int Register, LocatorValueType ValueType, ulong Value, ulong Detail = 0, string? Text = null)
{
    private const ulong PageMask = 0xFFFF_FFFF_FFFF;

    public PageAddress Page => new((short)(Value >> 32), (int)(uint)Value);

    public ushort Slot => (ushort)(Value >> 48);

    public ulong PageKey => Value & PageMask;

    public RowIdentifier Row => new(Page, Slot);

    public string SearchText => ColumnStoreObjectLocator.From(this) is { } columnStore ? columnStore.SearchText : ToString();

    public override string ToString() => ValueType switch
    {
        LocatorValueType.Row
            => $"({Page.FileId}:{Page.PageId}:{Slot})",
        LocatorValueType.ColumnStoreObject
            => new ColumnStoreObjectLocator(this).ToString(),
        LocatorValueType.LogSequenceNumber
            => new LogSequenceNumberLocator(this).ToString(),
        LocatorValueType.Text or LocatorValueType.WideText
            => new TextLocator(this).ToString(),
        LocatorValueType.TypeInfo
            => new TypeInfoLocator(this).ToString(),
        LocatorValueType.Variant
            => new VariantLocator(this).ToString(),
        _ => Page.ToString()
    };
}
