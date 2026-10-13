using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed record TraceArgumentCall(ulong Sequence,
                                       uint ThreadId,
                                       bool Returned,
                                       ulong[] IntegerSlots,
                                       ulong ReturnValue,
                                       int Node = -1)
{
    internal const int ValueCount = 8;

    internal const int RegisterCount = 4;

    private const int IntegerOffset = 2;

    private const int ReturnValueIndex = 6;

    private const int NodeIndex = 7;

    private const ulong ReturnedFlag = 1ul << 32;

    public IReadOnlyList<Locator> Locators { get; init; } = [];

    public ulong ReturnedValue => Returned ? ReturnValue : 0;

    public static bool IsCaptured(ArgumentSlot slot) => slot.Location == ArgumentLocation.Register;

    public static bool IsReturnCaptured(string returnType) => !ArgumentValue.IsFloating(returnType);

    public ulong? Value(ArgumentSlot slot) 
        => IsCaptured(slot) ? IntegerSlots[slot.Index] : null;

    public ulong Slot(int index) => index >= 0 && index < IntegerSlots.Length ? IntegerSlots[index] : 0;

    public Locator? GetLocator(int register)
    {
        foreach (var locator in Locators)
        {
            if (locator.Register == register)
            {
                return locator;
            }
        }

        return null;
    }

    internal static TraceArgumentCall Decode(ReadOnlySpan<ulong> values, IReadOnlyList<Locator>? locators = null)
    {
        var flags = values[1];

        return new TraceArgumentCall(values[0],
                                     (uint)flags,
                                     (flags & ReturnedFlag) != 0,
                                     [.. values.Slice(IntegerOffset, RegisterCount)],
                                     values[ReturnValueIndex],
                                     (int)values[NodeIndex])
        {
            Locators = locators ?? []
        };
    }
}
