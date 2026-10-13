using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class InstanceMethodCollector
{
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

    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> MethodLookup =
        Methods.GetAlternateLookup<ReadOnlySpan<char>>();

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

    public static bool IsIteratorMethod(ReadOnlySpan<char> symbol)
    {
        var separator = SymbolsHelper.FindClassMethodSeparator(symbol);

        return separator > 0
               && IsIteratorClass(symbol)
               && MethodLookup.Contains(symbol[(separator + 2)..]);
    }

    public static bool IsPlanObjectMethod(ReadOnlySpan<char> symbol)
        => SymbolsHelper.FindClassMethodSeparator(symbol) > 0 && IsPlanObjectClass(symbol);

    public static bool IsPlanObjectClass(ReadOnlySpan<char> className) => StartsWithAny(className, PlanObjectPrefixes);

    public static bool IsInstanceMethod(ReadOnlySpan<char> symbol) => IsIteratorMethod(symbol) || IsPlanObjectMethod(symbol);

    public static bool IsInstanceClass(ReadOnlySpan<char> symbol) => IsIteratorClass(symbol) || IsPlanObjectClass(symbol);

    public static List<InstanceMethod> FindInstanceMethods(IEnumerable<uint> addresses, DiaResolver resolver)
    {
        var methods = new List<InstanceMethod>();

        foreach (var rva in addresses)
        {
            if (InstanceRegister(resolver.SignaturesAt(rva).FirstOrDefault(), resolver.GetDecoratedName(rva)) is { } register)
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

            if (IsInstanceClass(parameter) && parameter.EndsWith('*'))
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsIteratorClass(ReadOnlySpan<char> symbol) => StartsWithAny(symbol, ClassPrefixes);

    private static bool StartsWithAny(ReadOnlySpan<char> value, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
