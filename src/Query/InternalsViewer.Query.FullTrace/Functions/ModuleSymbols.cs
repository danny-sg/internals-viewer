using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class ModuleSymbols
{
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
