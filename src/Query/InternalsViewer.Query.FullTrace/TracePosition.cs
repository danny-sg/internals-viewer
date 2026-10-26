using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace InternalsViewer.Query.FullTrace;

/// <summary>
/// Absolute position in the trace
/// </summary>
/// <remarks>
/// Packed into a 64-bit value with the following layout:
///
///     Bits  0-31: Steps    - Count of instructions on a thread since the last Sequence point
///     Bits 32-63: Sequence - Trace counter marking synchronisation across threads
///
/// Unknown has every bit set, so it sorts after every known position.
/// </remarks>
public readonly record struct TracePosition : IComparable<TracePosition>
{
    public const ulong MaximumSequence = uint.MaxValue - 1;

    private const int StepBits = 32;

    private const ulong StepMask = (1ul << StepBits) - 1;

    private const ulong UnknownSequence = ulong.MaxValue;

    private const ulong UnknownValue = ulong.MaxValue;

    public TracePosition(ulong sequence, ulong steps)
    {
        if (sequence == UnknownSequence)
        {
            Value = UnknownValue;

            return;
        }

        if (sequence > MaximumSequence || steps > StepMask)
        {
            ThrowOutOfRange(sequence, steps);
        }

        Value = (sequence << StepBits) | steps;
    }

    private TracePosition(ulong value) => Value = value;

    public static TracePosition Unknown { get; } = new(UnknownValue);

    public ulong Sequence => IsKnown ? Value >> StepBits : UnknownSequence;

    public ulong Steps => IsKnown ? Value & StepMask : 0;

    public bool IsKnown => Value != UnknownValue;

    public TracePosition Next => new(Sequence, Steps + 1);

    private ulong Value { get; }

    public static bool operator <(TracePosition left, TracePosition right) 
        => left.CompareTo(right) < 0;

    public static bool operator >(TracePosition left, TracePosition right) 
        => left.CompareTo(right) > 0;

    public static bool operator <=(TracePosition left, TracePosition right) 
        => left.CompareTo(right) <= 0;

    public static bool operator >=(TracePosition left, TracePosition right) 
        => left.CompareTo(right) >= 0;

    public int CompareTo(TracePosition other) => Value.CompareTo(other.Value);

    public override string ToString()
        => IsKnown ? string.Create(CultureInfo.InvariantCulture, $"{Sequence:X}:{Steps:X}") : "Unknown";

    [DoesNotReturn]
    private static void ThrowOutOfRange(ulong sequence, ulong steps)
        => throw new ArgumentOutOfRangeException(nameof(sequence),
                                                 string.Create(CultureInfo.InvariantCulture,
                                                               $"Trace position {sequence:X}:{steps:X} is outside the supported range"));
}
