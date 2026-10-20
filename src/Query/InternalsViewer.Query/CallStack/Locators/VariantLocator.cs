using InternalsViewer.Query.CallStack.Locators.Enums;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct VariantLocator(Locator Locator)
{
    private const ulong BooleanMask = 0xFF;

    public VariantStatus Status => (VariantStatus)(byte)Locator.Detail;

    public uint Length => (uint)(Locator.Detail >> 32);

    public ulong Value => Locator.Value;

    public static VariantLocator? From(Locator locator)
        => locator.ValueType == LocatorValueType.Variant ? new VariantLocator(locator) : null;

    public static Locator Create(int register, VariantStatus status, ulong value, uint length)
        => new(register, LocatorValueType.Variant, value, ((ulong)length << 32) | (byte)status);

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
