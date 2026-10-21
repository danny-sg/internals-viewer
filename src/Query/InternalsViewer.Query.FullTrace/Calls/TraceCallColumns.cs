using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Calls;

internal readonly struct TraceCallColumns(ulong[] values, int calls)
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

    public ulong[] Values => values;

    public int Calls => calls;

    public TracePosition GetFromPosition(int callIndex) => new(Get(FromSequenceIndex, callIndex), Get(FromStepsIndex, callIndex));

    public TracePosition GetToPosition(int callIndex) => new(Get(ToSequenceIndex, callIndex), Get(ToStepsIndex, callIndex));

    public uint GetThread(int callIndex) => (uint)Get(FlagsIndex, callIndex);

    public bool IsReturned(int callIndex) => (Get(FlagsIndex, callIndex) & ReturnedFlag) != 0;

    public ulong GetRegister(int callIndex, int slot) => Get(IntegerOffset + slot, callIndex);

    public ulong GetReturnValue(int callIndex) => Get(ReturnValueIndex, callIndex);

    public int GetCallNodeIndex(int callIndex) => (int)Get(NodeIndex, callIndex);

    public TraceArgumentCall GetCall(int callIndex, IReadOnlyList<Locator>? locators = null)
    {
        var registers = new ulong[ArgumentLayout.RegisterCount];

        for (var slot = 0; slot < registers.Length; slot++)
        {
            registers[slot] = GetRegister(callIndex, slot);
        }

        return new TraceArgumentCall(GetFromPosition(callIndex),
                                     GetThread(callIndex),
                                     IsReturned(callIndex),
                                     registers,
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

    private ulong Get(int column, int callIndex) => values[column * calls + callIndex];

    private void Set(int column, int callIndex, ulong value) => values[column * calls + callIndex] = value;
}
