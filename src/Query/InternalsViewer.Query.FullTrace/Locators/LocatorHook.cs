using System.Buffers.Binary;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class LocatorHook(IReadOnlyList<LocatorRead> reads, TraceCallStore.Builder store) : ITraceHook
{
    private const int ArgumentRegisters = 4;

    private const int ArgumentSlots = 16;

    private const int ReturnRegister = 16;

    private const int MaximumTextBytes = 128;

    public IEnumerable<TraceHookFunction> Functions
        => Reads.Keys.Select(f => new TraceHookFunction(f, ReturnReads.Contains(f)
                                                               ? TraceHookEvents.Entry | TraceHookEvents.Return
                                                               : TraceHookEvents.Entry));

    private Dictionary<ulong, LocatorRead[]> Reads { get; } = reads.GroupBy(r => r.Function).ToDictionary(g => g.Key, g => g.ToArray());

    private HashSet<ulong> ReturnReads { get; } = [.. reads.Where(r => r.IsReadAtReturn).Select(r => r.Function)];

    private Dictionary<ulong, LocatorStart> Starts { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Reads.TryGetValue(call.Function, out var functionReads))
        {
            return;
        }

        ulong[]? stackArguments = null;

        foreach (var read in functionReads)
        {
            if (read.Register is < 0 or >= ArgumentSlots)
            {
                continue;
            }

            if (read.Register >= ArgumentRegisters && stackArguments is null)
            {
                stackArguments = HookArguments.LoadStack(replay, call.StackPointer, call.ReturnAddress);
            }

            var pointer = read.Register < ArgumentRegisters
                ? call.Arguments[read.Register]
                : stackArguments![read.Register - ArgumentRegisters];

            if (read.ValueType == LocatorValueType.Argument)
            {
                Record(call, read, pointer, 0);
            }
            else if (!read.IsReadAtReturn)
            {
                ReadPointer(replay, call, read, pointer);
            }
        }

        if (ReturnReads.Contains(call.Function))
        {
            Starts[call.Id] = new LocatorStart([.. call.Arguments], stackArguments);
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.Id, out var start) || !Reads.TryGetValue(call.Function, out var functionReads))
        {
            return;
        }

        foreach (var read in functionReads)
        {
            if (!read.IsReadAtReturn || read.Register is < 0 or > ReturnRegister)
            {
                continue;
            }

            var pointer = read.Register == ReturnRegister ? call.ReturnValue
                          : read.Register < ArgumentRegisters ? start.Arguments[read.Register]
                          : start.StackArguments?[read.Register - ArgumentRegisters] ?? 0;

            ReadPointer(replay, call, read, pointer);
        }
    }

    public void OnAbandoned(in TraceHookCall call) => Starts.Remove(call.Id);

    private void ReadPointer(ITraceReplay replay, in TraceHookCall call, LocatorRead read, ulong pointer)
    {
        if (pointer == 0)
        {
            return;
        }

        foreach (var hop in read.Path ?? [])
        {
            if (!replay.TryRead(pointer + (uint)hop, out ulong next) || next == 0)
            {
                return;
            }

            pointer = next;
        }

        if (read.ValueType is LocatorValueType.Text or LocatorValueType.WideText)
        {
            ReadText(replay, call, read, pointer + (uint)read.Offset);

            return;
        }

        Span<byte> bytes = stackalloc byte[sizeof(ulong)];

        bytes.Clear();

        var known = replay.Read(pointer + (uint)read.Offset, bytes);

        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);

        var isBytes = read.ValueType == LocatorValueType.TypeInfo;

        if (isBytes && known is > 0 and < sizeof(ulong))
        {
            Record(call, read, value & ((1ul << (known * 8)) - 1), (uint)known);

            return;
        }

        if (known != sizeof(ulong))
        {
            return;
        }

        Record(call, read, value, isBytes ? (uint)known : 0);
    }

    private void ReadText(ITraceReplay replay, in TraceHookCall call, LocatorRead read, ulong address)
    {
        Span<ulong> words = stackalloc ulong[MaximumTextBytes / sizeof(ulong)];

        words.Clear();

        var characters = MemoryMarshal.AsBytes(words);

        var bytes = replay.Read(address, characters);

        if (bytes == 0)
        {
            return;
        }

        var width = read.ValueType == LocatorValueType.WideText ? 2 : 1;

        var end = bytes;

        for (var index = 0; index + width <= bytes; index += width)
        {
            if (characters[index] == 0 && (width == 1 || characters[index + 1] == 0))
            {
                end = index + width;

                break;
            }
        }

        var parts = (end + sizeof(ulong) - 1) / sizeof(ulong);

        for (var part = 0; part < parts; part++)
        {
            Record(call, read, words[part], (uint)part);
        }
    }

    private void Record(in TraceHookCall call, LocatorRead read, ulong value, uint part)
        => store.AddLocators([new PointerValue(call.Function,
                                               call.Instance,
                                               value,
                                               call.Call,
                                               read.Register,
                                               (uint)read.Offset,
                                               read.IsReadAtReturn ? 1u : 0u,
                                               read.Mode,
                                               part)]);

    private sealed record LocatorStart(ulong[] Arguments, ulong[]? StackArguments);
}
