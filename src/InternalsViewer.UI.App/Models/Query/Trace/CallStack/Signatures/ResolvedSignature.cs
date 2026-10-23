using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures;

public sealed record ResolvedSignature(FunctionSignature Parsed, string? Text, ArgumentLayout Layout);
