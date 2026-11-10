using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Locators;

public readonly record struct Locator(int ArgumentSlot, LocatorValueType ValueType, ulong Value, ulong Detail = 0, string? Text = null)
{
    private const ulong PageMask = 0xFFFF_FFFF_FFFF;

    public PageAddress Page => new((short)(Value >> 32), (int)(uint)Value);

    public ushort Slot => (ushort)(Value >> 48);

    public ulong PageKey => Value & PageMask;

    public RowIdentifier Row => new(Page, Slot);

    public string SearchText => ValueType == LocatorValueType.ColumnStoreObject ? new ColumnStoreObjectLocator(this).SearchText : ToString();

    public static Locator? Read(LocatorValueType valueType, IMemoryReader memory, int argumentSlot, ulong address, ulong count = 1)
        => valueType switch
    {
        LocatorValueType.Bindings
            => BindingLocator.Read(memory, argumentSlot, address, count),
        LocatorValueType.ColumnStoreObject
            => ColumnStoreObjectLocator.Read(memory, argumentSlot, address),
        LocatorValueType.LogSequenceNumber
            => LogSequenceNumberLocator.Read(memory, argumentSlot, address),
        LocatorValueType.Text or LocatorValueType.WideText
            => TextLocator.Read(memory, argumentSlot, address, valueType),
        LocatorValueType.TypeInfo
            => TypeInfoLocator.Read(memory, argumentSlot, address),
        LocatorValueType.Variant
            => VariantLocator.Read(memory, argumentSlot, address),
        _ => memory.TryRead(address, out ulong value) ? new Locator(argumentSlot, valueType, value) : null
    };

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
        LocatorValueType.Bindings
            => new BindingLocator(this).ToString(),
        _ => Page.ToString()
    };
}
