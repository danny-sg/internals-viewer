using System.Buffers.Binary;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class TypeInfoLocatorParser : LocatorParser
{
    private const int DetailOffset = 16;

    public override LocatorValueType ValueType => LocatorValueType.TypeInfo;

    public override IReadOnlyList<LocatorTypeName> TypeNames { get; } = [new("CTypeInfo")];

    public override Locator? Read(ITraceReplay replay, int register, ulong address)
        => ReadBytes(replay, address) is { } header
            ? TypeInfoLocator.Create(register, header, ReadBytes(replay, address + DetailOffset) ?? 0)
            : null;

    private static ulong? ReadBytes(ITraceReplay replay, ulong address)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];

        bytes.Clear();

        var known = replay.Read(address, bytes);

        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);

        return known switch
        {
            0 => null,
            < sizeof(ulong) => value & ((1ul << (known * 8)) - 1),
            _ => value
        };
    }
}
