using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class MemoryMapFunctionResolver
{
    private const string ConstructorPrefix = "??0";

    public static List<MemoryMapFunction> Find(DiaResolver resolver, IEnumerable<uint> functions)
    {
        var found = new List<MemoryMapFunction>();

        Span<char> buffer = stackalloc char[DiaResolver.MaximumNameLength];

        foreach (var rva in functions)
        {
            if (!resolver.TryGetDecoratedName(rva, buffer, out var name)
                || FunctionSignature.KindOf(new string(name)) != FunctionKind.Member)
            {
                continue;
            }

            var operation = name.StartsWith(ConstructorPrefix, StringComparison.Ordinal) && !IsFolded(resolver, rva)
                ? MemoryMapOperation.Constructed
                : MemoryMapOperation.Visited;

            found.Add(new MemoryMapFunction(rva, (uint)operation));
        }

        return found;
    }

    private static bool IsFolded(DiaResolver resolver, uint rva)
        => resolver.EnumerateSymbolsAtRva(rva).Where(d => d.IsFunction).Select(d => d.Name).Distinct().Skip(1).Any();
}
