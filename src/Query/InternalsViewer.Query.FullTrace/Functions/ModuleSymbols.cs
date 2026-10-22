using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class ModuleSymbols
{
    public static void VisitFunctions(DiaResolver resolver, IReadOnlySet<uint> functions, Action<FunctionSymbol> visit)
    {
        var first = new Dictionary<uint, (int Name, int? Parameters)>();

        var cache = new FunctionSymbol.SignatureCache();

        Span<char> buffer = stackalloc char[DiaResolver.MaximumNameLength];

        using var symbols = resolver.ReadSymbols();

        while (symbols.MoveNext())
        {
            var rva = symbols.Rva;

            if (!symbols.IsFunction || !functions.Contains(rva))
            {
                continue;
            }

            var exists = first.TryGetValue(rva, out var seen);

            scoped ReadOnlySpan<char> decoratedName = default;

            if (!exists && !resolver.TryGetDecoratedName(rva, buffer, out decoratedName))
            {
                continue;
            }

            var signature = symbols.Signature;

            var name = string.GetHashCode(symbols.Name);

            int? parameters = signature.IsEmpty ? null : FunctionSignature.HashParameters(signature);

            if (!exists)
            {
                seen = (name, parameters);
            }
            else if (seen.Parameters is null)
            {
                seen.Parameters = parameters;
            }

            first[rva] = seen;

            cache.Reset();

            visit(new FunctionSymbol
            {
                Rva = rva,
                Name = symbols.Name,
                Signature = signature,
                DecoratedName = decoratedName,
                IsFirst = !exists,
                HasSameName = seen.Name == name,
                HasSameParameters = parameters is null || seen.Parameters == parameters,
                Cache = cache
            });
        }
    }

    public static Dictionary<uint, FunctionClassification> Load(DiaResolver resolver,
                                                                Func<uint, ReadOnlySpan<char>, FunctionClassification> classify,
                                                                Action<uint, ReadOnlySpan<char>> visitData)
    {
        var functions = new Dictionary<uint, FunctionClassification>();

        using var symbols = resolver.ReadSymbols(includeSignature: false);

        while (symbols.MoveNext())
        {
            if (symbols.IsFunction)
            {
                Add(functions, symbols.Rva, classify(symbols.Rva, symbols.Name));
            }
            else
            {
                visitData(symbols.Rva, symbols.Name);
            }
        }

        return functions;
    }

    public static void Add(Dictionary<uint, FunctionClassification> functions, uint rva, FunctionClassification classification)
    {
        ref var existing = ref CollectionsMarshal.GetValueRefOrAddDefault(functions, rva, out var exists);

        existing = exists ? existing.Merge(classification) : classification;
    }
}
