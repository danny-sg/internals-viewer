using System.Globalization;

namespace InternalsViewer.Query.FullTrace;

/// <summary>
/// Absolution position in the trace
/// </summary>
/// <remarks>
/// Sequence - Trace counter marking syncronization across threads
/// Steps    - Count of instructions on a thread since the last Sequence point
/// </remarks>
public readonly record struct TracePosition(ulong Sequence, ulong Steps) : IComparable<TracePosition>
{
    private const ulong UnknownSequence = ulong.MaxValue;

    public static TracePosition Unknown { get; } = new(UnknownSequence, 0);

    public bool IsKnown => Sequence != UnknownSequence;

    public static bool operator <(TracePosition left, TracePosition right) 
        => left.CompareTo(right) < 0;

    public static bool operator >(TracePosition left, TracePosition right) 
        => left.CompareTo(right) > 0;

    public static bool operator <=(TracePosition left, TracePosition right) 
        => left.CompareTo(right) <= 0;

    public static bool operator >=(TracePosition left, TracePosition right) 
        => left.CompareTo(right) >= 0;

    public int CompareTo(TracePosition other)
        => Sequence != other.Sequence ? Sequence.CompareTo(other.Sequence) : Steps.CompareTo(other.Steps);

    public override string ToString()
        => IsKnown ? string.Create(CultureInfo.InvariantCulture, $"{Sequence:X}:{Steps:X}") : "Unknown";
}
