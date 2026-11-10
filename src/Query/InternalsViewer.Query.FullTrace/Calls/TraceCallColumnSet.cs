using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

internal readonly struct TraceCallColumnSet(ulong[] values, int callCount, int stackSlotCount = 0)
{
    public const int ValueCount = 11;

    public const int FromSequenceIndex = 0;

    public const int FromStepsIndex = 1;

    public const int FlagsIndex = 2;

    public const int IntegerOffset = 3;

    public const int ReturnValueIndex = 7;

    public const int NodeIndex = 8;

    public const int ToSequenceIndex = 9;

    public const int ToStepsIndex = 10;

    public const ulong ReturnedFlag = 1ul << 32;

    private const ulong UnknownSequence = ulong.MaxValue;

    public ulong[] Values => values;

    public int CallCount => callCount;

    public int StackSlotCount => stackSlotCount;

    public TracePosition GetFromPosition(int callIndex) => new(Get(FromSequenceIndex, callIndex), Get(FromStepsIndex, callIndex));

    public TracePosition GetToPosition(int callIndex) => new(Get(ToSequenceIndex, callIndex), Get(ToStepsIndex, callIndex));

    public uint GetThread(int callIndex) => (uint)Get(FlagsIndex, callIndex);

    public bool IsReturned(int callIndex) => (Get(FlagsIndex, callIndex) & ReturnedFlag) != 0;

    public ulong GetRegister(int callIndex, int slot) => Get(IntegerOffset + slot, callIndex);

    public ulong? GetStackValue(int callIndex, int position)
        => position - ArgumentLayout.RegisterCount is >= 0 and var slot && slot < stackSlotCount ? Get(ValueCount + slot, callIndex) : null;

    public ulong GetReturnValue(int callIndex) => Get(ReturnValueIndex, callIndex);

    public int GetCallNodeIndex(int callIndex) => (int)Get(NodeIndex, callIndex);

    public TraceArgumentCall GetCall(int callIndex, IReadOnlyList<Locator>? locators = null)
    {
        var slots = new ulong[ArgumentLayout.RegisterCount + stackSlotCount];

        for (var slot = 0; slot < ArgumentLayout.RegisterCount; slot++)
        {
            slots[slot] = GetRegister(callIndex, slot);
        }

        for (var slot = 0; slot < stackSlotCount; slot++)
        {
            slots[ArgumentLayout.RegisterCount + slot] = Get(ValueCount + slot, callIndex);
        }

        return new TraceArgumentCall(GetFromPosition(callIndex),
                                     GetThread(callIndex),
                                     IsReturned(callIndex),
                                     slots,
                                     GetReturnValue(callIndex),
                                     GetCallNodeIndex(callIndex))
        {
            Locators = locators ?? [],
            ToPosition = GetToPosition(callIndex)
        };
    }

    public void SetToPosition(int callIndex, TracePosition toPosition, ulong returnValue, bool returned)
    {
        Set(ToSequenceIndex, callIndex, toPosition.Sequence);

        Set(ToStepsIndex, callIndex, toPosition.Steps);

        if (returned)
        {
            Set(ReturnValueIndex, callIndex, returnValue);

            Set(FlagsIndex, callIndex, Get(FlagsIndex, callIndex) | ReturnedFlag);
        }
    }

    public static void Encode(Span<ulong> values, int callCount)
    {
        var fromSequences = values.Slice(FromSequenceIndex * callCount, callCount);

        var toSequences = values.Slice(ToSequenceIndex * callCount, callCount);

        for (var callIndex = callCount - 1; callIndex >= 0; callIndex--)
        {
            if (toSequences[callIndex] != UnknownSequence)
            {
                toSequences[callIndex] -= fromSequences[callIndex];
            }

            if (callIndex > 0)
            {
                fromSequences[callIndex] -= fromSequences[callIndex - 1];
            }
        }
    }

    public static void Decode(Span<ulong> values, int callCount)
    {
        var fromSequences = values.Slice(FromSequenceIndex * callCount, callCount);

        var toSequences = values.Slice(ToSequenceIndex * callCount, callCount);

        for (var callIndex = 0; callIndex < callCount; callIndex++)
        {
            if (callIndex > 0)
            {
                fromSequences[callIndex] += fromSequences[callIndex - 1];
            }

            if (toSequences[callIndex] != UnknownSequence)
            {
                toSequences[callIndex] += fromSequences[callIndex];
            }
        }
    }

    private ulong Get(int column, int callIndex) => values[column * callCount + callIndex];

    private void Set(int column, int callIndex, ulong value) => values[column * callCount + callIndex] = value;
}
