using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack.TimeTravel.Iterators;

internal sealed class IteratorMethods
{
    private const string Module = "sqlmin";

    private static readonly string[] ClassPrefixes = ["CQScan", "CBpQScan", "CQueryScan"];

    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "Open",
        "GetRow",
        "GetRowOrReQualifyHelper",
        "Close",
        "BpOpen",
        "BpGetNextBatch",
        "BpClose"
    };

    private HashSet<uint> Found { get; } = [];

    private HashSet<uint> Shared { get; } = [];

    public static bool AppliesTo(string module) => string.Equals(module, Module, StringComparison.OrdinalIgnoreCase);

    public static bool TakesIterator(string? signature, string? decoratedName)
    {
        if (signature is null || decoratedName is null)
        {
            return true;
        }

        return FunctionSignature.Parse(signature, decoratedName) switch
        {
            { Kind: FunctionKind.Member } => true,
            { Kind: FunctionKind.Static, Parameters: [var first, ..] } => IsIteratorClass(first) && first.TrimEnd().EndsWith('*'),
            _ => false
        };
    }

    public static bool IsIteratorMethod(string symbol)
    {
        var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol);

        return separator > 0
               && IsIteratorClass(symbol)
               && Methods.Contains(symbol[(separator + 2)..]);
    }

    public void Add(SymbolDetail symbol)
    {
        if (IsIteratorMethod(symbol.Name))
        {
            Found.Add(symbol.Rva);
        }
        else if (!IsIteratorClass(symbol.Name))
        {
            Shared.Add(symbol.Rva);
        }
    }

    public List<uint> Resolve(DiaResolver resolver)
    {
        Found.ExceptWith(Shared);

        return [.. Found.Where(rva => TakesIterator(Signature(resolver, rva), resolver.GetDecoratedName(rva)))];
    }

    private static string? Signature(DiaResolver resolver, uint rva)
        => resolver.EnumerateSymbolsAtRva(rva)
                   .Where(s => s.IsFunction && s.Signature.Length > 0)
                   .Select(s => s.Signature)
                   .FirstOrDefault();

    private static bool IsIteratorClass(string symbol)
        => ClassPrefixes.Any(prefix => symbol.StartsWith(prefix, StringComparison.Ordinal));
}
