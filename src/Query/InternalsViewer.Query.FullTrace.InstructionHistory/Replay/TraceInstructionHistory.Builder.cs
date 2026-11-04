using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

public sealed partial class TraceInstructionHistory
{
    internal sealed class Builder
    {
        private List<TraceInstructionStep> Steps { get; } = [];

        private List<TraceRegisterChange> Changes { get; } = [];

        private Dictionary<ulong, byte[]> Code { get; } = [];

        public void Add(ReadOnlySpan<TraceInstructionStep> steps, ReadOnlySpan<TraceRegisterChange> changes)
        {
            var firstChange = Changes.Count;

            foreach (var change in changes)
            {
                Changes.Add(change);
            }

            foreach (var step in steps)
            {
                Steps.Add(step with { FirstChange = firstChange + step.FirstChange });
            }
        }

        public void AddCode(ulong address, ReadOnlySpan<byte> bytes)
        {
            if (!Code.ContainsKey(address))
            {
                Code[address] = bytes.ToArray();
            }
        }

        public TraceInstructionHistory Build(TraceInstructionOutcome outcome) => new(Steps, Changes, Code, outcome);
    }
}
