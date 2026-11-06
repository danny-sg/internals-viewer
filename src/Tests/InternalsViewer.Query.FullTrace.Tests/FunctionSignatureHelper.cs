using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests;

internal static class FunctionSignatureHelper
{
    public static FunctionSignature? Parse(ReadOnlySpan<char> signature, ReadOnlySpan<char> decoratedName)
    {
        var parameters = new List<string>();

        return FunctionSignature.TryParse(signature,
                                          decoratedName,
                                          null,
                                          parameters,
                                          out var type,
                                          out var returnType,
                                          out var hasReturnBuffer)
            ? new FunctionSignature(parameters, type, returnType, hasReturnBuffer)
            : null;
    }
}
