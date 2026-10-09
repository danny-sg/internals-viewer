using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.CallStack.TimeTravel.CallLog;

public sealed record TimeTravelArgumentCall(ulong Sequence,
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

    public static bool IsCaptured(ArgumentSlot slot) => slot.Location == ArgumentLocation.Register;

    public static bool IsReturnCaptured(string returnType) => !ArgumentValue.IsFloating(returnType);

    public ulong? Value(ArgumentSlot slot) => IsCaptured(slot) ? IntegerSlots[slot.Index] : null;

    internal static TimeTravelArgumentCall From(ReadOnlySpan<ulong> values)
    {
        var flags = values[1];

        return new TimeTravelArgumentCall(values[0],
                                          (uint)flags,
                                          (flags & ReturnedFlag) != 0,
                                          values.Slice(IntegerOffset, RegisterCount).ToArray(),
                                          values[ReturnValueIndex],
                                          (int)values[NodeIndex]);
    }
}
