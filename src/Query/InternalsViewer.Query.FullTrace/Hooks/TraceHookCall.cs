namespace InternalsViewer.Query.FullTrace.Hooks;

internal readonly ref struct TraceHookCall
{
    public ulong Function { get; init; }

    public ulong Instance { get; init; }

    public ulong Id { get; init; }

    public uint CallIndex { get; init; }

    public uint Thread { get; init; }

    public int CallNodeIndex { get; init; }

    public ReadOnlySpan<ulong> Arguments { get; init; }

    public ulong ReturnValue { get; init; }

    public ulong StackPointer { get; init; }

    public ulong ReturnAddress { get; init; }

    public TracePosition Position { get; init; }

    public ulong Instructions { get; init; }
}
