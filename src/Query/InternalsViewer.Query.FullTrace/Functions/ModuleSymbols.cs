using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Reads symbols from a module's PDB via DIA
/// </summary>
/// <remarks>
/// Multiple symbols can resolve to the same RVA (for example identical functions folded together by the linker), so both operations handle
/// duplicate addresses:
///
///     - VisitFunctions - enumerates function symbols for a set of RVAs of interest and calls the visitor with a
///       <see cref="FunctionSymbol"/> for each. Name and parameter hashes of the first symbol at each address are tracked so
///       revisits can report whether they match (IsFirst/HasSameName/HasSameParameters). The decorated name is only resolved
///       for the first symbol at an address, and a single stack buffer and signature cache are reused across the enumeration
///       to avoid allocations.
///
///     - Load - enumerates every symbol, classifying functions into a per RVA <see cref="FunctionClassification"/> map (merging duplicates
///       via Add) and passing non function symbols to the data visitor.
/// </remarks>
internal static class ModuleSymbols
{
    /// <summary>
    /// Enumerates symbol records and invokes a callback for each function where the RVA is in the functions set
    /// </summary>
    /// <remarks>
    /// The visit delegate is called for every matching symbol, including duplicates at the same RVA. The decorated (mangled) name is only
    /// resolved for the first symbol at an address. If it cannot be resolved the symbol is skipped.
    ///
    /// Revisited symbols report their relationship to the first via IsFirst/HasSameName/HasSameParameters.
    /// </remarks>
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

    /// <summary>
    /// Enumerates all symbols, classifying functions and passing non-function symbols to the visitData callback
    /// </summary>
    /// <remarks>
    /// The return Functions list gives unique list of RVA/classification.
    ///
    /// The visitData callback is 'everthing else' which include types
    /// </remarks>
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
