using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Locators;

public readonly record struct VariantLocator(Locator Locator)
{
    public const ulong Size = 0x20;

    private const ulong StatusOffset = 0x00;

    private const ulong ValueOffset = 0x08;

    private const ulong LengthOffset = 0x10;

    private const ulong BooleanMask = 0xFF;

    public VariantStatus Status => (VariantStatus)(byte)Locator.Detail;

    public uint Length => (uint)(Locator.Detail >> 32);

    public ulong Value => Locator.Value;

    public static Locator? Read(IMemoryReader memory, int argumentSlot, ulong address)
    {
        if (!memory.TryRead(address + StatusOffset, out byte status) || !memory.TryRead(address + ValueOffset, out ulong value))
        {
            return null;
        }

        var length = memory.TryRead(address + LengthOffset, out uint found) ? found : 0;

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
