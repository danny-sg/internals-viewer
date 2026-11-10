using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Hooks;

/// <summary>
/// Call Argument Hook
/// </summary>
/// <remarks>
/// Reads the arguments named by the function and read pairs it is built with. Functions lists those addresses for the bridge, adding the
/// Return event when any read on that function has IsReadAtReturn set.
///
/// Argument values are always taken at entry, because the argument registers are not preserved across the call.
/// 
/// IsReadAtReturn only delays the read through the value, so an out parameter is read after the call has filled it.
///
/// Arguments are indexed 0-16.
///
/// - 0-3 are registers, captured by the native bridge at entry:
/// 
///     0 = RCX
///     1 = RDX
///     2 = R8
///     3 = R9
///
/// - 4-15 are stack slots, read past the return address and the 0x20 home space (RSP + 0x28, RSP + 0x30, etc.), or past the home space
///   alone (RSP + 0x20 onwards) when the return address is no longer at RSP
///
/// - 16 is the return value (RAX), only available at return
/// </remarks>
internal abstract class ArgumentHook<TRead> : ITraceHook
    where TRead : IArgumentRead
{
    protected ArgumentHook(IEnumerable<(ulong Function, TRead Read)> reads)
    {
        var ranges = reads.TryGetNonEnumeratedCount(out var count)
            ? new Dictionary<ulong, (int Start, int Count)>(count)
            : new Dictionary<ulong, (int Start, int Count)>();

        foreach (var (function, _) in reads)
        {
            ref var range = ref CollectionsMarshal.GetValueRefOrAddDefault(ranges, function, out _);

            range.Count++;
        }

        var next = 0;

        foreach (var function in ranges.Keys)
        {
            ref var range = ref CollectionsMarshal.GetValueRefOrNullRef(ranges, function);

            var total = range.Count;

            range = (next, 0);

            next += total;
        }

        var all = new TRead[next];

        foreach (var (function, read) in reads)
        {
            ref var range = ref CollectionsMarshal.GetValueRefOrNullRef(ranges, function);

            all[range.Start + range.Count++] = read;
        }

        Ranges = ranges;

        Reads = all;
    }

    public IEnumerable<TraceHookFunction> Functions
        => Ranges.Select(r => new TraceHookFunction(r.Key, HasReturnRead(r.Value)
                                                           ? TraceHookEvents.Entry | TraceHookEvents.Return
                                                           : TraceHookEvents.Entry));

    private Dictionary<ulong, (int Start, int Count)> Ranges { get; }

    private TRead[] Reads { get; }

    private Dictionary<ulong, ulong[]> Starts { get; } = [];

    private Stack<ulong[]> FreeArguments { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Ranges.TryGetValue(call.FunctionAddress, out var range))
        {
            return;
        }

        var functionReads = Reads.AsSpan(range.Start, range.Count);

        var isStackRead = false;

        var isReadAtReturn = false;

        foreach (var read in functionReads)
        {
            isStackRead |= read.ArgumentSlot is >= ArgumentLayout.RegisterCount and < ArgumentLayout.CapturedSlotCount;

            isReadAtReturn |= read.IsReadAtReturn;
        }

        Span<ulong> arguments = stackalloc ulong[ArgumentLayout.CapturedSlotCount];

        LoadArguments(call, replay, arguments, isStackRead);

        foreach (var read in functionReads)
        {
            if (!read.IsReadAtReturn)
            {
                Read(replay, call, read, GetArgument(arguments, call, read.ArgumentSlot), arguments);
            }
        }

        if (isReadAtReturn)
        {
            var saved = FreeArguments.TryPop(out var free) ? free : new ulong[ArgumentLayout.CapturedSlotCount];

            arguments.CopyTo(saved);

            Starts[call.CallId] = saved;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.CallId, out var arguments))
        {
            return;
        }

        if (Ranges.TryGetValue(call.FunctionAddress, out var range))
        {
            foreach (var read in Reads.AsSpan(range.Start, range.Count))
            {
                if (read.IsReadAtReturn)
                {
                    Read(replay, call, read, GetArgument(arguments, call, read.ArgumentSlot), arguments);
                }
            }
        }

        FreeArguments.Push(arguments);
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        if (Starts.Remove(call.CallId, out var arguments))
        {
            FreeArguments.Push(arguments);
        }
    }

    protected static ulong GetArgument(ReadOnlySpan<ulong> arguments, in TraceHookCall call, int argumentSlot)
        => argumentSlot switch
        {
            ArgumentLayout.ReturnArgumentSlot
                => call.ReturnValue,
            >= 0 and < ArgumentLayout.CapturedSlotCount
                => arguments[argumentSlot],
            _ => 0
        };

    protected abstract void Read(ITraceReplay replay, in TraceHookCall call, TRead read, ulong pointer, ReadOnlySpan<ulong> arguments);

    private static void LoadArguments(in TraceHookCall call, ITraceReplay replay, Span<ulong> arguments, bool isStackRead)
    {
        call.Arguments.CopyTo(arguments);

        var stack = arguments[ArgumentLayout.RegisterCount..];

        stack.Clear();

        if (!isStackRead)
        {
            return;
        }

        var read = replay.Read(TraceHookArgumentReader.GetStackArguments(replay, call), MemoryMarshal.AsBytes(stack)) / sizeof(ulong);

        stack[read..].Clear();
    }

    private bool HasReturnRead((int Start, int Count) range)
    {
        foreach (var read in Reads.AsSpan(range.Start, range.Count))
        {
            if (read.IsReadAtReturn)
            {
                return true;
            }
        }

        return false;
    }
}
