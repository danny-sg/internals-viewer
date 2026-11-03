using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed class NamedFunctionCollector<T>(Dictionary<string, T> names)
{
    private Dictionary<string, T>.AlternateLookup<ReadOnlySpan<char>> Names { get; } = names.GetAlternateLookup<ReadOnlySpan<char>>();

    private Dictionary<ulong, T> Functions { get; } = [];

    public void VisitFunction(uint rva, ReadOnlySpan<char> name)
    {
        if (Names.TryGetValue(name, out var type))
        {
            Functions.TryAdd(rva, type);
        }
    }

    public AddressTable<T> Build() => new(Functions);
}
