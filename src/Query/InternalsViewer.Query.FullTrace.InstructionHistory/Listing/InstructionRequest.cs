using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

public sealed record InstructionRequest(CallFrame Frame, uint Thread, TracePosition From, TracePosition To);
