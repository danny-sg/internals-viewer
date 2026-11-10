using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Symbols.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols;

public readonly struct Symbol
{
    internal Symbol(SymbolTable table, int index)
    {
        Table = table;
        Index = index;
    }

    public uint Rva => Table.GetRva(Index);

    public bool IsFunction => Table.IsFunction(Index);

    public FunctionType? FunctionType => Table.GetFunctionType(Index);

    public string Name => Table.GetName(Index);

    public string ClassName => Table.GetClassName(Index);

    public string MethodName => Table.GetSeparator(Index) is > 0 and var separator ? Name[(separator + 2)..] : Name;

    public string Signature => Table.GetSignature(Index);

    public FunctionSignature? Parsed => Table.GetParsedSignature(Index);

    internal SymbolTable Table { get; }

    internal int Index { get; }
}
