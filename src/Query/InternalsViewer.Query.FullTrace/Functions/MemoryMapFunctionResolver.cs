using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Dia;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;

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
                || FunctionSignature.GetFunctionType(name) != FunctionType.Member)
            {
                continue;
            }

            var operation = name.StartsWith(ConstructorPrefix, StringComparison.Ordinal) && !IsFolded(resolver, rva)
                ? MemoryMapOperation.Constructed
                : MemoryMapOperation.Visited;

            found.Add(new MemoryMapFunction(rva, operation));
        }

        return found;
    }

    private static bool IsFolded(DiaResolver resolver, uint rva)
    {
        Span<char> first = stackalloc char[DiaResolver.MaximumNameLength];

        var firstLength = -1;

        using var symbols = resolver.ReadSymbolsAtRva(rva, includeSignature: false);

        while (symbols.MoveNext())
        {
            if (!symbols.IsFunction)
            {
                continue;
            }

            if (firstLength < 0)
            {
                symbols.Name.CopyTo(first);

                firstLength = symbols.Name.Length;
            }
            else if (!symbols.Name.SequenceEqual(first[..firstLength]))
            {
                return true;
            }
        }

        return false;
    }
}
