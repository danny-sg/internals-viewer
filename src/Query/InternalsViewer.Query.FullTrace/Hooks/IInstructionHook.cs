using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Hooks;

internal interface IInstructionHook
{
    void OnInstructionChunk(ReadOnlySpan<InstructionStep> steps,
                            ReadOnlySpan<RegisterChange> changes,
                            ReadOnlySpan<InstructionCode> code,
                            ReadOnlySpan<byte> bytes);
}
