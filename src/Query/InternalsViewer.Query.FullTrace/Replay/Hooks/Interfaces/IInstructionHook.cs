using InternalsViewer.Query.FullTrace.Replay.Native;

namespace InternalsViewer.Query.FullTrace.Replay.Hooks.Interfaces;

internal interface IInstructionHook
{
    void OnInstructionChunk(ReadOnlySpan<InstructionStep> steps,
                            ReadOnlySpan<RegisterChange> changes,
                            ReadOnlySpan<InstructionCode> code,
                            ReadOnlySpan<byte> bytes);
}
