using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.CallStack.TimeTravel.Iterators;

internal sealed class InstanceMethodCollector
{
    private const string Module = "sqlmin";

    private const int RegisterCount = 4;

    private static readonly string[] ClassPrefixes = ["CQScan", "CBpQScan", "CQueryScan"];

    private static readonly string[] PlanObjectPrefixes = ["CXte", "CBpXte"];

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

    public static int? InstanceRegister(string? signature, string? decoratedName)
    {
        if (signature is null || decoratedName is null)
        {
            return 0;
        }

        return FunctionSignature.Parse(signature, decoratedName) switch
        {
            { Kind: FunctionKind.Member } => 0,
            { Kind: FunctionKind.Static, Parameters: var parameters } => KeyedParameter(parameters),
            _ => null
        };
    }

    public static bool IsIteratorMethod(string symbol)
    {
        var separator = ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol);

        return separator > 0
               && IsIteratorClass(symbol)
               && Methods.Contains(symbol[(separator + 2)..]);
    }

    public static bool IsPlanObjectMethod(string symbol)
        => ResolvedCallstackFrameParser.FindClassMethodSeparator(symbol) > 0 && IsPlanObjectClass(symbol);

    public static bool IsPlanObjectClass(string? className)
        => className is not null && PlanObjectPrefixes.Any(prefix => className.StartsWith(prefix, StringComparison.Ordinal));

    public void Add(SymbolDetail symbol)
    {
        if (IsIteratorMethod(symbol.Name) || IsPlanObjectMethod(symbol.Name))
        {
            Found.Add(symbol.Rva);
        }
        else if (!IsIteratorClass(symbol.Name) && !IsPlanObjectClass(symbol.Name))
        {
            Shared.Add(symbol.Rva);
        }
    }

    public List<InstanceMethod> Resolve(DiaResolver resolver)
    {
        Found.ExceptWith(Shared);

        var methods = new List<InstanceMethod>();

        foreach (var rva in Found)
        {
            if (InstanceRegister(Signature(resolver, rva), resolver.GetDecoratedName(rva)) is { } register)
            {
                methods.Add(new InstanceMethod(rva, register));
            }
        }

        return methods;
    }

    private static int? KeyedParameter(IReadOnlyList<string> parameters)
    {
        for (var index = 0; index < Math.Min(parameters.Count, RegisterCount); index++)
        {
            var parameter = parameters[index].TrimEnd();

            if ((IsIteratorClass(parameter) || IsPlanObjectClass(parameter)) && parameter.EndsWith('*'))
            {
                return index;
            }
        }

        return null;
    }

    private static string? Signature(DiaResolver resolver, uint rva)
        => resolver.EnumerateSymbolsAtRva(rva)
                   .Where(s => s.IsFunction && s.Signature.Length > 0)
                   .Select(s => s.Signature)
                   .FirstOrDefault();

    private static bool IsIteratorClass(string symbol)
        => ClassPrefixes.Any(prefix => symbol.StartsWith(prefix, StringComparison.Ordinal));
}
