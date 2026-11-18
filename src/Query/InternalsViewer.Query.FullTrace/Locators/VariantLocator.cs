using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Locators;

public readonly record struct VariantLocator(Locator Locator)
{
    private const ulong BooleanMask = 0xFF;

    public VariantStatus Status => (VariantStatus)(byte)Locator.Detail;

    public uint Length => (uint)(Locator.Detail >> 32);

    public ulong Value => Locator.Value;

    public static Locator? Read(ITraceMemoryReader memory, int argumentSlot, ulong address)
    {
        var layout = memory.Layout.RowBuffer;

        if (!memory.TryRead(address + (ulong)layout.StatusField.Offset, out byte status)
            || !memory.TryRead(address + (ulong)layout.ValueField.Offset, out ulong value))
        {
            return null;
        }

        var length = memory.TryRead(address + (ulong)layout.LengthField.Offset, out uint found) ? found : 0;

        return Create(argumentSlot, (VariantStatus)status, value, length);
    }

    public static Locator Create(int argumentSlot, VariantStatus status, ulong value, uint length)
        => new(argumentSlot, LocatorValueType.Variant, value, ((ulong)length << 32) | (byte)status);

    public bool TryGetBoolean(out bool value)
    {
        var low = Value & BooleanMask;

        value = low == 1;

        return Status == VariantStatus.Value && low <= 1;
    }

    public override string ToString() => Status switch
    {
        VariantStatus.Null => "NULL",
        VariantStatus.Value when Length == 0 => $"0x{Value:X} ({Value})",
        VariantStatus.Value => $"0x{Value:X}",
        _ => $"Status 0x{(byte)Status:X2}"
    };
}
