using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceArgumentCall(TracePosition FromPosition,
                                       uint ThreadId,
                                       bool Returned,
                                       ulong[] IntegerSlots,
                                       ulong ReturnValue,
                                       int CallNodeIndex = -1)
{
    public IReadOnlyList<Locator> Locators { get; init; } = [];

    public TracePosition ToPosition { get; init; } = TracePosition.Unknown;

    public ulong ReturnedValue => Returned ? ReturnValue : 0;

    public static bool IsCaptured(ArgumentSlot slot) => slot.Location is ArgumentLocation.Register or ArgumentLocation.Stack;

    public static bool IsReturnCaptured(string returnType) => !ArgumentValue.IsFloating(returnType);

    public ulong? GetValue(ArgumentSlot slot) => slot.Location switch
    {
        ArgumentLocation.Register 
            => IntegerSlots[slot.Index],
        ArgumentLocation.Stack 
            => GetStackValue(slot.Index),
        _ => null
    };

    public ulong? GetStackValue(int position)
    {
        foreach (var locator in Locators)
        {
            if (locator.Register == position && locator.ValueType == LocatorValueType.Argument)
            {
                return locator.Value;
            }
        }

        return null;
    }

    public ulong Slot(int index) => index >= 0 && index < IntegerSlots.Length ? IntegerSlots[index] : 0;

    public Locator? GetLocator(int register)
    {
        foreach (var locator in Locators)
        {
            if (locator.Register == register && locator.ValueType != LocatorValueType.Argument)
            {
                return locator;
            }
        }

        return null;
    }
}
