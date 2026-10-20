using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct LogSequenceNumberLocator(Locator Locator)
{
    public LogSequenceNumber Lsn => new((int)(uint)Locator.Value, (int)(Locator.Value >> 32), (short)(ushort)Locator.Detail);

    public static Locator? Read(IMemoryReader memory, int register, ulong address)
        => memory.TryRead(address, out ulong first) && memory.TryRead(address + sizeof(ulong), out ulong second)
            ? Create(register, first, second)
            : null;

    public static Locator Create(int register, ulong first, ulong second)
        => new(register, LocatorValueType.LogSequenceNumber, first, (ushort)second);

    public override string ToString() => Lsn.ToString();
}
