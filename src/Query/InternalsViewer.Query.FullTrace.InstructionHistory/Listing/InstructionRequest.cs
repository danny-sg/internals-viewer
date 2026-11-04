using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

public sealed record InstructionRequest(CallstackFrame Frame, uint Thread, TracePosition From, TracePosition To);
