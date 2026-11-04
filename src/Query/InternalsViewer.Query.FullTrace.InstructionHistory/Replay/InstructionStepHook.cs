using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay.Enums;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Replay;

internal sealed class InstructionStepHook : ITraceHook
{
    public IEnumerable<TraceHookFunction> Functions => [];

    public TraceInstructionHistory.Builder Builder { get; } = new();

    public void OnInstructionChunk(ReadOnlySpan<InstructionStep> steps,
                                   ReadOnlySpan<RegisterChange> changes,
                                   ReadOnlySpan<InstructionCode> code,
                                   ReadOnlySpan<byte> bytes)
    {
        var replayedSteps = new TraceInstructionStep[steps.Length];

        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];

            replayedSteps[index] = new TraceInstructionStep(new TracePosition(step.Sequence, step.Steps),
                                                            step.ProgramCounter,
                                                            (int)step.FirstChange,
                                                            (int)step.ChangeCount,
                                                            step.Depth);
        }

        var replayedChanges = new TraceRegisterChange[changes.Length];

        for (var index = 0; index < changes.Length; index++)
        {
            var change = changes[index];

            replayedChanges[index] = new TraceRegisterChange((TraceRegister)change.Register, change.Low, change.High);
        }

        Builder.Add(replayedSteps, replayedChanges);

        foreach (var entry in code)
        {
            Builder.AddCode(entry.Address, bytes.Slice((int)entry.Offset, (int)entry.Length));
        }
    }
}
