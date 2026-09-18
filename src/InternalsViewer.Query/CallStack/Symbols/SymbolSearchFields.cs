namespace InternalsViewer.Query.CallStack.Symbols;

/// <summary>
/// The parts of a symbol a search text is matched against
/// </summary>
[Flags]
public enum SymbolSearchFields
{
    None = 0,
    Module = 1,
    Class = 2,
    Signature = 4,
    All = Module | Class | Signature
}
