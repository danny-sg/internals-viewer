using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class ModuleSymbols
{
    public static Dictionary<uint, FunctionClassification> Load(DiaResolver resolver,
                                                                Func<uint, ReadOnlySpan<char>, FunctionClassification> classify)
    {
        var functions = new Dictionary<uint, FunctionClassification>();

        resolver.VisitFunctions((rva, name) => Add(functions, rva, classify(rva, name)));

        return functions;
    }

    public static void Add(Dictionary<uint, FunctionClassification> functions, uint rva, FunctionClassification classification)
    {
        ref var existing = ref CollectionsMarshal.GetValueRefOrAddDefault(functions, rva, out var exists);

        existing = exists ? existing.Merge(classification) : classification;
    }
}
