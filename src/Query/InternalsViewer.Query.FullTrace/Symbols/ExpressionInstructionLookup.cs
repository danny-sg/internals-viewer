namespace InternalsViewer.Query.FullTrace.Symbols;

internal static class ExpressionInstructionLookup
{
    private const string Module = "SqlTsEs";

    private const string Parameters = "(CEsExec *,CXVariant *)";

    private const int LoadCount = -1;

    private const string ManipData = "ManipData";

    private const string SetMultData = "SetMultData";

    public static bool IsInstruction(string module, string signature)
        => module.Equals(Module, StringComparison.OrdinalIgnoreCase) && signature.EndsWith(Parameters, StringComparison.Ordinal);

    public static bool IsOperandAccess(int count, string? handler) => count == LoadCount || handler is ManipData or SetMultData;
}
