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

    public TracePosition GetFromPosition(int call) => new(Get(FromSequenceIndex, call), Get(FromStepsIndex, call));

    public TracePosition GetToPosition(int call) => new(Get(ToSequenceIndex, call), Get(ToStepsIndex, call));

    public uint GetThread(int call) => (uint)Get(FlagsIndex, call);

    public bool IsReturned(int call) => (Get(FlagsIndex, call) & ReturnedFlag) != 0;

    public ulong GetRegister(int call, int slot) => Get(IntegerOffset + slot, call);

    public ulong GetReturnValue(int call) => Get(ReturnValueIndex, call);

    public int GetNodeIndex(int call) => (int)Get(NodeIndex, call);

    public TraceArgumentCall GetCall(int call, IReadOnlyList<Locator>? locators = null)
    {
        var registers = new ulong[ArgumentLayout.RegisterCount];

        for (var slot = 0; slot < registers.Length; slot++)
        {
            registers[slot] = GetRegister(call, slot);
        }

        return new TraceArgumentCall(GetFromPosition(call),
                                     GetThread(call),
                                     IsReturned(call),
                                     registers,
                                     GetReturnValue(call),
                                     GetNodeIndex(call))
        {
            Locators = locators ?? [],
            ToPosition = GetToPosition(call)
        };
    }

    public void SetToPosition(int call, TracePosition toPosition, ulong returnValue, bool returned)
    {
        Set(ToSequenceIndex, call, toPosition.Sequence);

        Set(ToStepsIndex, call, toPosition.Steps);

        if (returned)
        {
            Set(ReturnValueIndex, call, returnValue);

            Set(FlagsIndex, call, Get(FlagsIndex, call) | ReturnedFlag);
        }
    }

    private ulong Get(int column, int call) => values[column * calls + call];

    private void Set(int column, int call, ulong value) => values[column * calls + call] = value;
}
