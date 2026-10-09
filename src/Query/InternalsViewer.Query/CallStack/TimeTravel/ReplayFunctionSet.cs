using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record ReplayFunctionSet(ulong[] Excluded, MemoryFunction[] Memory);
