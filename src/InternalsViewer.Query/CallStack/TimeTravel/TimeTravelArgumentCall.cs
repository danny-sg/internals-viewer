using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelArgumentCall(ulong Sequence,
                                            uint ThreadId,
                                            bool Returned,
                                            bool StackRead,
                                            ulong[] IntegerSlots,
                                            ulong[] FloatingSlots,
                                            ulong?[] PointeesOnEntry,
                                            ulong?[] PointeesOnReturn,
                                            ulong ReturnValue,
                                            ulong FloatingReturnValue,
                                            int Node = -1)
{
    internal const int ValueCount = 33;

    private const int IntegerOffset = 2;

    private const int FloatingOffset = 10;

    private const int EntryOffset = 14;

    private const int ReturnOffset = 22;

    private const int ReturnValueIndex = 30;

    private const int FloatingReturnValueIndex = 31;

    private const int NodeIndex = 32;

    private const int FloatingRegisters = 4;

    private const ulong ReturnedFlag = 1ul << 32;

    private const ulong StackReadFlag = 1ul << 33;

    private const int EntryFlagShift = 40;

    private const int ReturnFlagShift = 48;

    public ulong? Value(ArgumentSlot slot) => slot.Location switch
    {
        ArgumentLocation.Register => IntegerSlots[slot.Index],
        ArgumentLocation.Stack when StackRead => IntegerSlots[slot.Index],
        ArgumentLocation.FloatingRegister => FloatingSlots[slot.Index],
        _ => null
    };

    public ulong? Pointee(ArgumentSlot slot, bool onReturn)
        => slot.Index is >= 0 and < ArgumentLayout.CapturedSlots
            ? onReturn ? PointeesOnReturn[slot.Index] : PointeesOnEntry[slot.Index]
            : null;

    internal static TimeTravelArgumentCall From(ReadOnlySpan<ulong> values)
    {
        var flags = values[1];

        return new TimeTravelArgumentCall(values[0],
                                          (uint)flags,
                                          (flags & ReturnedFlag) != 0,
                                          (flags & StackReadFlag) != 0,
                                          values.Slice(IntegerOffset, ArgumentLayout.CapturedSlots).ToArray(),
                                          values.Slice(FloatingOffset, FloatingRegisters).ToArray(),
                                          Pointees(values.Slice(EntryOffset, ArgumentLayout.CapturedSlots), flags, EntryFlagShift),
                                          Pointees(values.Slice(ReturnOffset, ArgumentLayout.CapturedSlots), flags, ReturnFlagShift),
                                          values[ReturnValueIndex],
                                          values[FloatingReturnValueIndex],
                                          (int)values[NodeIndex]);
    }

    private static ulong?[] Pointees(ReadOnlySpan<ulong> values, ulong flags, int shift)
    {
        var pointees = new ulong?[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            pointees[i] = (flags & (1ul << (shift + i))) != 0 ? values[i] : null;
        }

        return pointees;
    }
}
