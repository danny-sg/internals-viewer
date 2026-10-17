using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.RowBuffers.HashRows.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers.HashRows;

internal sealed record HashRowFunctions(IReadOnlyDictionary<ulong, HashRowFunctionType> Functions, ulong[] HashIterators)
{
    public static HashRowFunctions Empty { get; } = new(new Dictionary<ulong, HashRowFunctionType>(), []);

    public static HashRowFunctions Merge(IEnumerable<HashRowFunctions> functions)
    {
        var all = functions.ToList();

        return new(AddressMap.Merge(all.Select(f => f.Functions)), [.. all.SelectMany(f => f.HashIterators)]);
    }

    public HashRowFunctions Rebase(ulong address)
        => new(AddressMap.Rebase(Functions, address), [.. HashIterators.Select(v => v + address)]);

    internal sealed class Collector
    {
        private const string HashClass = "CQScanHash";

        private const string VTableName = "`vftable'";

        private static readonly Dictionary<string, HashRowFunctionType> FunctionTypes = new(StringComparer.Ordinal)
        {
            ["CQScanHash::GetRow"] = HashRowFunctionType.GetRow,
            ["CHashWorkfilePartitionGroup::Lookup"] = HashRowFunctionType.Lookup,
            ["CHashWorkfilePartitionGroup::Insert"] = HashRowFunctionType.Insert
        };

        private static Dictionary<string, HashRowFunctionType>.AlternateLookup<ReadOnlySpan<char>> FunctionLookup { get; }
            = FunctionTypes.GetAlternateLookup<ReadOnlySpan<char>>();

        private Dictionary<ulong, HashRowFunctionType> Functions { get; } = [];

        private List<ulong> VTables { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            if (FunctionLookup.TryGetValue(name, out var type))
            {
                Functions.TryAdd(rva, type);
            }
        }

        public void VisitData(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var memberName);

            if (className is HashClass && memberName.StartsWith(VTableName, StringComparison.Ordinal))
            {
                VTables.Add(rva);
            }
        }

        public HashRowFunctions Build()
            => Functions.ContainsValue(HashRowFunctionType.GetRow) && Functions.ContainsValue(HashRowFunctionType.Lookup)
                ? new(Functions, [.. VTables.Distinct()])
                : Empty;
    }
}
