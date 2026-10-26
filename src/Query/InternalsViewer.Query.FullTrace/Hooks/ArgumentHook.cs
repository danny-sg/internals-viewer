using System.Runtime.InteropServices;
using InternalsViewer.Query.Extensions;
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
internal abstract class ArgumentHook<TRead>(IEnumerable<(ulong Function, TRead Read)> reads) : ITraceHook
    where TRead : IArgumentRead
{
    public IEnumerable<TraceHookFunction> Functions
        => Reads.Select(r => new TraceHookFunction(r.Key, r.Value.Any(f => f.IsReadAtReturn)
                                                          ? TraceHookEvents.Entry | TraceHookEvents.Return
                                                          : TraceHookEvents.Entry));

    private Dictionary<ulong, TRead[]> Reads { get; }
        = reads.GroupBy(r => r.Function).ToDictionary(g => g.Key, g => g.Select(r => r.Read).ToArray());

    private Dictionary<ulong, ulong[]> Starts { get; } = [];

    private Stack<ulong[]> FreeArguments { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Reads.TryGetValue(call.FunctionAddress, out var functionReads))
        {
            return;
        }

        var isStackRead = false;

        var isReadAtReturn = false;

        foreach (var read in functionReads)
        {
            isStackRead |= read.Register is >= ArgumentLayout.RegisterCount and < ArgumentLayout.CapturedSlots;

            isReadAtReturn |= read.IsReadAtReturn;
        }

        Span<ulong> arguments = stackalloc ulong[ArgumentLayout.CapturedSlots];

        LoadArguments(call, replay, arguments, isStackRead);

        foreach (var read in functionReads)
        {
            if (!read.IsReadAtReturn)
            {
                Read(replay, call, read, GetArgument(arguments, call, read.Register), arguments);
            }
        }

        if (isReadAtReturn)
        {
            var saved = FreeArguments.TryPop(out var free) ? free : new ulong[ArgumentLayout.CapturedSlots];

            arguments.CopyTo(saved);

            Starts[call.Id] = saved;
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.Id, out var arguments))
        {
            return;
        }

        if (Reads.TryGetValue(call.FunctionAddress, out var functionReads))
        {
            foreach (var read in functionReads)
            {
                if (read.IsReadAtReturn)
                {
                    Read(replay, call, read, GetArgument(arguments, call, read.Register), arguments);
                }
            }
        }

        FreeArguments.Push(arguments);
    }

    public void OnAbandoned(in TraceHookCall call)
    {
        if (Starts.Remove(call.Id, out var arguments))
        {
            FreeArguments.Push(arguments);
        }
    }

    protected static ulong GetArgument(ReadOnlySpan<ulong> arguments, in TraceHookCall call, int register)
        => register switch
        {
            ArgumentLayout.ReturnRegister
                => call.ReturnValue,
            >= 0 and < ArgumentLayout.CapturedSlots
                => arguments[register],
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

        var read = replay.Read(GetStackArguments(replay, call), MemoryMarshal.AsBytes(stack)) / sizeof(ulong);

        stack[read..].Clear();
    }

    private static ulong GetStackArguments(ITraceReplay replay, in TraceHookCall call)
    {
        var hasReturnAddress = replay.TryRead(call.StackPointer, out ulong top) && top == call.ReturnAddress;

        return call.StackPointer + (ulong)(hasReturnAddress ? ArgumentLayout.StackArgumentOffset : ArgumentLayout.HomeSpaceSize);
    }
}
