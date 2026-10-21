using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class ModuleSymbols
{
    public static void VisitFunctions(DiaResolver resolver, IReadOnlySet<uint> functions, Action<FunctionSymbol> visit)
    {
        var found = new List<(uint Rva, string Name, string Signature)>();

        using (var symbols = resolver.ReadSymbols())
        {
            while (symbols.MoveNext())
            {
                if (symbols.IsFunction && functions.Contains(symbols.Rva))
                {
                    found.Add((symbols.Rva, symbols.Name.ToString(), symbols.Signature.ToString()));
                }
            }
        }

        var first = new Dictionary<uint, (int Name, int? Parameters)>();

        Span<char> buffer = stackalloc char[DiaResolver.MaximumNameLength];

        foreach (var (rva, symbolName, signature) in found)
        {
            var exists = first.TryGetValue(rva, out var seen);

            var decoratedName = string.Empty;

            if (!exists)
            {
                if (!resolver.TryGetDecoratedName(rva, buffer, out var decorated))
                {
                    continue;
                }

                decoratedName = decorated.ToString();
            }

            var name = string.GetHashCode(symbolName);

            int? parameters = signature.Length == 0 ? null : FunctionSignature.HashParameters(signature);

            if (!exists)
            {
                seen = (name, parameters);
            }
            else if (seen.Parameters is null)
            {
                seen.Parameters = parameters;
            }

            first[rva] = seen;

            visit(new FunctionSymbol
            {
                Rva = rva,
                Name = symbolName,
                Signature = signature,
                DecoratedName = decoratedName,
                IsFirst = !exists,
                HasSameName = seen.Name == name,
                HasSameParameters = parameters is null || seen.Parameters == parameters
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
