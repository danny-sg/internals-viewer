using System.Buffers.Binary;
using System.Text;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorHook(IEnumerable<LocatorRead> reads, TraceCallStore.Builder store, ImageText? images = null)
    : ArgumentHook<LocatorRead>(reads.Select(r => (r.Function, r)))
{
    private const int MaximumTextBytes = 128;

    private const int TypeInfoDetailOffset = 16;

    protected override void Read(ITraceReplay replay, in TraceHookCall call, LocatorRead read, ulong pointer)
    {
        var locator = read.ValueType == LocatorValueType.Argument
            ? new Locator(read.Register, read.ValueType, pointer)
            : ReadValue(replay, read, pointer);

        if (locator is { } found)
        {
            store.AddLocator(call.Function, call.Instance, (int)call.Call, found, read.IsReadAtReturn);
        }
    }

    private static bool TryFollow(ITraceReplay replay, int[]? path, ref ulong pointer)
    {
        foreach (var hop in path ?? [])
        {
            if (!replay.TryRead(pointer + (uint)hop, out ulong next) || next == 0)
            {
                return false;
            }

            pointer = next;
        }

        return true;
    }

    private static Locator? ReadTypeInfo(ITraceReplay replay, int register, ulong address)
        => ReadBytes(replay, address) is { } header
            ? new Locator(register, LocatorValueType.TypeInfo, header, ReadBytes(replay, address + TypeInfoDetailOffset) ?? 0)
            : null;

    private static Locator? ReadLogSequenceNumber(ITraceReplay replay, int register, ulong address)
        => replay.TryRead(address, out ulong first) && replay.TryRead(address + sizeof(ulong), out ulong second)
            ? Locator.CreateLogSequenceNumber(register, first, second)
            : null;

    private static Locator? ReadColumnStoreObject(ITraceReplay replay, int register, ulong address)
        => replay.TryRead(address, out ulong first)
           && replay.TryRead(address + sizeof(ulong), out ulong second)
           && replay.TryRead(address + 2 * sizeof(ulong), out ulong third)
            ? Locator.ColumnStoreObject(register, first, second, third)
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

    private static int FindTerminator(ReadOnlySpan<byte> bytes, int width)
    {
        for (var index = 0; index + width <= bytes.Length; index += width)
        {
            if (bytes[index] == 0 && (width == 1 || bytes[index + 1] == 0))
            {
                return index;
            }
        }

        return -1;
    }

    private Locator? ReadValue(ITraceReplay replay, LocatorRead read, ulong pointer)
    {
        if (pointer == 0 || !TryFollow(replay, read.Path, ref pointer))
        {
            return null;
        }

        var address = pointer + (uint)read.Offset;

        return read.ValueType switch
        {
            LocatorValueType.Text or LocatorValueType.WideText => ReadText(replay, read, address),
            LocatorValueType.TypeInfo => ReadTypeInfo(replay, read.Register, address),
            LocatorValueType.LogSequenceNumber => ReadLogSequenceNumber(replay, read.Register, address),
            LocatorValueType.ColumnStoreObject => ReadColumnStoreObject(replay, read.Register, address),
            _ => replay.TryRead(address, out ulong value) ? new Locator(read.Register, read.ValueType, value) : null
        };
    }

    private Locator? ReadText(ITraceReplay replay, LocatorRead read, ulong address)
    {
        var isWide = read.ValueType == LocatorValueType.WideText;

        Span<byte> bytes = stackalloc byte[MaximumTextBytes];

        bytes.Clear();

        var count = replay.Read(address, bytes);

        var data = bytes[..((count + sizeof(ulong) - 1) / sizeof(ulong) * sizeof(ulong))];

        var end = FindTerminator(data, isWide ? 2 : 1);

        if ((count == 0 || end < 0)
            && read.Path is not { Length: > 0 }
            && images?.Read(address, isWide) is { } imageText)
        {
            return new Locator(read.Register, read.ValueType, (ulong)imageText.Length, 0, imageText);
        }

        if (count == 0)
        {
            return null;
        }

        var characters = end < 0 ? data : data[..end];

        var text = isWide ? Encoding.Unicode.GetString(characters) : Encoding.Latin1.GetString(characters);

        return new Locator(read.Register, read.ValueType, (ulong)text.Length, end < 0 ? 1ul : 0, text);
    }
}
