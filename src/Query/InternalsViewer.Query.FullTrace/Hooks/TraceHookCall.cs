namespace InternalsViewer.Query.FullTrace.Hooks;

internal readonly ref struct TraceHookCall
{
    public ulong Function { get; init; }

    public ulong Instance { get; init; }

    public ulong Id { get; init; }

    public ulong CallCount { get; init; }

    public uint Call { get; init; }

    public uint Thread { get; init; }

    public ReadOnlySpan<ulong> Arguments { get; init; }

    public ulong ReturnValue { get; init; }

    public ReadOnlySpan<ulong> Callers { get; init; }
}
