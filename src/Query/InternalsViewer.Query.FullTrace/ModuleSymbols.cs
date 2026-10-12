using InternalsViewer.Query.CallStack.Dia;

namespace InternalsViewer.Query.FullTrace;

internal sealed class ModuleSymbols(IReadOnlyDictionary<uint, string[]> functions)
{
    public IEnumerable<uint> Addresses => Functions.Keys;

    private IReadOnlyDictionary<uint, string[]> Functions { get; } = functions;

    public static ModuleSymbols Load(DiaResolver resolver)
        => new(resolver.EnumerateSymbolDetails(string.Empty, includeSignature: false)
                       .Where(s => s.IsFunction)
                       .GroupBy(s => s.Rva)
                       .ToDictionary(g => g.Key, g => g.Select(s => s.Name).Distinct().ToArray()));

    public IEnumerable<uint> FindAddresses(Func<IReadOnlyList<string>, bool> names)
        => Functions.Where(f => names(f.Value)).Select(f => f.Key);

    public IEnumerable<(uint Rva, T Value)> ClassifyStrict<T>(Func<uint, string, T?> classify) where T : class
    {
        foreach (var (rva, names) in Functions)
        {
            if (GetStrictClassification(rva, names, classify) is { } value)
            {
                yield return (rva, value);
            }
        }
    }

    private static T? GetStrictClassification<T>(uint rva, string[] names, Func<uint, string, T?> classify) where T : class
    {
        T? result = null;

        foreach (var name in names)
        {
            if (classify(rva, name) is not { } value || (result is not null && !result.Equals(value)))
            {
                return null;
            }

            result = value;
        }

        return result;
    }
}
