using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Locators;

public readonly record struct TypeInfoLocator(Locator Locator)
{
    public SqlTypeInfo TypeInfo => SqlTypeInfo.Create(Locator.Value, Locator.Detail);

    public static Locator? Read(ITraceMemoryReader memory, int argumentSlot, ulong address)
    {
        var layout = memory.Layout.TypeInfo;

        if (ReadBytes(memory, address + (ulong)layout.TypeField.Offset) is not { } header)
        {
            return null;
        }

        var details = ReadBytes(memory, address + (ulong)layout.TypeDetailsField.Offset) ?? 0;

        return new Locator(argumentSlot, LocatorValueType.TypeInfo, header, details);
    }

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
