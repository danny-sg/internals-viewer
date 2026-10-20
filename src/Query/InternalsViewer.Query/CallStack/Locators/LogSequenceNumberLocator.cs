using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct LogSequenceNumberLocator(Locator Locator)
{
    public LogSequenceNumber Lsn => new((int)(uint)Locator.Value, (int)(Locator.Value >> 32), (short)(ushort)Locator.Detail);

    public static LogSequenceNumberLocator? From(Locator locator)
        => locator.ValueType == LocatorValueType.LogSequenceNumber ? new LogSequenceNumberLocator(locator) : null;

    public static Locator Create(int register, ulong first, ulong second)
        => new(register, LocatorValueType.LogSequenceNumber, first, (ushort)second);

    public override string ToString() => Lsn.ToString();
}
