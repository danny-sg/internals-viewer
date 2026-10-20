using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct Locator(int Register, LocatorValueType ValueType, ulong Value, ulong Detail = 0, string? Text = null)
{
    private const ulong PageMask = 0xFFFF_FFFF_FFFF;

    public PageAddress Page => new((short)(Value >> 32), (int)(uint)Value);

    public ushort Slot => (ushort)(Value >> 48);

    public ulong PageKey => Value & PageMask;

    public RowIdentifier Row => new(Page, Slot);

    public string SearchText => ValueType == LocatorValueType.ColumnStoreObject ? new ColumnStoreObjectLocator(this).SearchText : ToString();

    public static Locator? Read(LocatorValueType valueType, IMemoryReader memory, int register, ulong address) => valueType switch
    {
        LocatorValueType.ColumnStoreObject
            => ColumnStoreObjectLocator.Read(memory, register, address),
        LocatorValueType.LogSequenceNumber
            => LogSequenceNumberLocator.Read(memory, register, address),
        LocatorValueType.Text or LocatorValueType.WideText
            => TextLocator.Read(memory, register, address, valueType),
        LocatorValueType.TypeInfo
            => TypeInfoLocator.Read(memory, register, address),
        LocatorValueType.Variant
            => VariantLocator.Read(memory, register, address),
        _ => memory.TryRead(address, out ulong value) ? new Locator(register, valueType, value) : null
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
        _ => Page.ToString()
    };
}
