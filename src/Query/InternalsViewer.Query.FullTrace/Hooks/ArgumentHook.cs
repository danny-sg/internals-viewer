using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal abstract class ArgumentHook<TRead>(IEnumerable<(ulong Function, TRead Read)> reads) : ITraceHook
    where TRead : IArgumentRead
{
    private const int RegisterCount = 4;
    private const int SlotCount = 16;

    public IEnumerable<TraceHookFunction> Functions
        => Reads.Select(r => new TraceHookFunction(r.Key, r.Value.Any(f => f.IsReadAtReturn)
                                                          ? TraceHookEvents.Entry | TraceHookEvents.Return
                                                          : TraceHookEvents.Entry));

    private Dictionary<ulong, TRead[]> Reads { get; }
        = reads.GroupBy(r => r.Function).ToDictionary(g => g.Key, g => g.Select(r => r.Read).ToArray());

    private Dictionary<ulong, ulong[]> Starts { get; } = [];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Reads.TryGetValue(call.Function, out var functionReads))
        {
            return;
        }

        var isStackRead = false;

        var isReadAtReturn = false;

        foreach (var read in functionReads)
        {
            isStackRead |= read.Register is >= RegisterCount and < SlotCount;

            isReadAtReturn |= read.IsReadAtReturn;
        }

        Span<ulong> arguments = stackalloc ulong[SlotCount];

        LoadArguments(call, replay, arguments, isStackRead);

        foreach (var read in functionReads)
        {
            if (!read.IsReadAtReturn)
            {
                Read(replay, call, read, GetArgument(arguments, call, read.Register));
            }
        }

        if (isReadAtReturn)
        {
            Starts[call.Id] = [.. arguments];
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.Id, out var arguments) || !Reads.TryGetValue(call.Function, out var functionReads))
        {
            return;
        }

        foreach (var read in functionReads)
        {
            if (read.IsReadAtReturn)
            {
                Read(replay, call, read, GetArgument(arguments, call, read.Register));
            }
        }
    }

    public void OnAbandoned(in TraceHookCall call) => Starts.Remove(call.Id);

    protected abstract void Read(ITraceReplay replay, in TraceHookCall call, TRead read, ulong pointer);

    private static void LoadArguments(in TraceHookCall call, ITraceReplay replay, Span<ulong> arguments, bool isStackRead)
    {
        call.Arguments.CopyTo(arguments);

        var stack = arguments[RegisterCount..];

        stack.Clear();

        if (!isStackRead)
        {
            return;
        }

        var read = replay.Read(replay.GetStackArguments(call), MemoryMarshal.AsBytes(stack)) / sizeof(ulong);

        stack[read..].Clear();
    }

    private static ulong GetArgument(ReadOnlySpan<ulong> arguments, in TraceHookCall call, int register)
        => register switch
        {
            LocatorReadResolver.ReturnRegister
                => call.ReturnValue,
            >= 0 and < SlotCount
                => arguments[register],
            _ => 0
        };
}
