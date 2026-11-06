using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures;

public sealed record ResolvedSignature(FunctionSignature Parsed, string? Text, ArgumentLayout Layout);
