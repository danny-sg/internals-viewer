using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed record ResolvedSignature(FunctionSignature Parsed, string? Text, ArgumentLayout Layout);
