using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

public sealed partial class TraceInstructionHistory
{
    private TraceInstructionHistory(List<TraceInstructionStep> steps,
                                    List<TraceRegisterChange> changes,
                                    Dictionary<ulong, byte[]> code,
                                    TraceInstructionOutcome outcome)
    {
        Steps = steps;
        Changes = changes;
        Code = code;
        Outcome = outcome;
    }

    public static TraceInstructionHistory Empty { get; } = new([], [], [], TraceInstructionOutcome.ReachedEnd);

    public List<TraceInstructionStep> Steps { get; }

    public TraceInstructionOutcome Outcome { get; }

    public int AddressCount => Code.Count;

    private List<TraceRegisterChange> Changes { get; }

    private Dictionary<ulong, byte[]> Code { get; }

    public ReadOnlySpan<TraceRegisterChange> GetChanges(int step)
    {
        var found = Steps[step];

        return CollectionsMarshal.AsSpan(Changes).Slice(found.FirstChange, found.ChangeCount);
    }

    public ReadOnlySpan<byte> GetCode(ulong address) => Code.TryGetValue(address, out var bytes) ? bytes : [];

    public List<ulong> GetAddresses() => [.. Code.Keys.Order()];

    public TraceRegisterState GetRegisters(int step)
    {
        var state = new TraceRegisterState();

        for (var index = 0; index <= step; index++)
        {
            foreach (var change in GetChanges(index))
            {
                state.Apply(change);
            }
        }

        return state;
    }
}
