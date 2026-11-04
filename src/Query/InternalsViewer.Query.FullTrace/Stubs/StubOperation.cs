using InternalsViewer.Query.FullTrace.Stubs.Enums;

namespace InternalsViewer.Query.FullTrace.Stubs;

/// <summary>
/// Decoded stub instruction
/// </summary>
/// <remarks>
/// Displacement is the memory-operand displacement for loads and memory jumps, or the folded immediate for add/subtract operations (negated
/// for subtract), so all forms evaluate as source + displacement.
/// </remarks>
internal readonly record struct StubOperation(StubOperationType Type, 
                                              int DestinationRegister, 
                                              int SourceRegister, 
                                              long Displacement, 
                                              ulong Address)
{
    public const int AbsoluteSource = -1;

    public bool IsAbsolute => SourceRegister == AbsoluteSource;
}
