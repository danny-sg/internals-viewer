using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

internal readonly struct TraceCallChunk(ulong[] values, int calls, int stackSlots = 0)
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

    public int Calls => calls;

    public int StackSlots => stackSlots;

    public TracePosition GetFromPosition(int callIndex) => new(Get(FromSequenceIndex, callIndex), Get(FromStepsIndex, callIndex));

    public TracePosition GetToPosition(int callIndex) => new(Get(ToSequenceIndex, callIndex), Get(ToStepsIndex, callIndex));

    public uint GetThread(int callIndex) => (uint)Get(FlagsIndex, callIndex);

    public bool IsReturned(int callIndex) => (Get(FlagsIndex, callIndex) & ReturnedFlag) != 0;

    public ulong GetRegister(int callIndex, int slot) => Get(IntegerOffset + slot, callIndex);

    public ulong? GetStackValue(int callIndex, int position)
        => position - ArgumentLayout.RegisterCount is >= 0 and var slot && slot < stackSlots ? Get(ValueCount + slot, callIndex) : null;

    public ulong GetReturnValue(int callIndex) => Get(ReturnValueIndex, callIndex);

    public int GetCallNodeIndex(int callIndex) => (int)Get(NodeIndex, callIndex);

    public TraceArgumentCall GetCall(int callIndex, IReadOnlyList<Locator>? locators = null)
    {
        var slots = new ulong[ArgumentLayout.RegisterCount + stackSlots];

        for (var slot = 0; slot < ArgumentLayout.RegisterCount; slot++)
        {
            slots[slot] = GetRegister(callIndex, slot);
        }

        for (var slot = 0; slot < stackSlots; slot++)
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

    public static void Encode(Span<ulong> values, int calls)
    {
        var fromSequences = values.Slice(FromSequenceIndex * calls, calls);

        var toSequences = values.Slice(ToSequenceIndex * calls, calls);

        for (var callIndex = calls - 1; callIndex >= 0; callIndex--)
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

    public static void Decode(Span<ulong> values, int calls)
    {
        var fromSequences = values.Slice(FromSequenceIndex * calls, calls);

        var toSequences = values.Slice(ToSequenceIndex * calls, calls);

        for (var callIndex = 0; callIndex < calls; callIndex++)
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

    private ulong Get(int column, int callIndex) => values[column * calls + callIndex];

    private void Set(int column, int callIndex, ulong value) => values[column * calls + callIndex] = value;
}
