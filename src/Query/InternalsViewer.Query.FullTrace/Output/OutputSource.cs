namespace InternalsViewer.Query.FullTrace.Output;

internal readonly record struct OutputSource(ulong Value)
{
    public static OutputSource Unknown { get; } = new(0);

    public int Slot { get; init; } = -1;

    public ulong Context { get; init; }

    public ulong Owner { get; init; }
}
