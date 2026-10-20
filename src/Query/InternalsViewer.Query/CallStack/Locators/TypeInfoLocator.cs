using System.Buffers.Binary;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct TypeInfoLocator(Locator Locator)
{
    private const int DetailOffset = 16;

    public SqlTypeInfo TypeInfo => SqlTypeInfo.From(Locator.Value, Locator.Detail);

    public static Locator? Read(IMemoryReader memory, int register, ulong address)
        => ReadBytes(memory, address) is { } header
            ? new Locator(register, LocatorValueType.TypeInfo, header, ReadBytes(memory, address + DetailOffset) ?? 0)
            : null;

    public override string ToString() => TypeInfo.ToString();

    private static ulong? ReadBytes(IMemoryReader memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];

        bytes.Clear();

        var known = memory.Read(address, bytes);

        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);

        return known switch
        {
            0 => null,
            < sizeof(ulong) => value & ((1ul << (known * 8)) - 1),
            _ => value
        };
    }
}
