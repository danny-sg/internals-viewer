using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers.HashRows;

internal sealed record HashRowFunctions(ulong[] GetRows, ulong[] Lookups, ulong[] Inserts, ulong[] HashIterators)
{
    public static HashRowFunctions Empty { get; } = new([], [], [], []);

    public static HashRowFunctions Merge(IEnumerable<HashRowFunctions> functions)
    {
        var all = functions.ToList();

        return new([.. all.SelectMany(f => f.GetRows)],
                   [.. all.SelectMany(f => f.Lookups)],
                   [.. all.SelectMany(f => f.Inserts)],
                   [.. all.SelectMany(f => f.HashIterators)]);
    }

    public HashRowFunctions Rebase(ulong address)
        => new([.. GetRows.Select(f => f + address)],
               [.. Lookups.Select(f => f + address)],
               [.. Inserts.Select(f => f + address)],
               [.. HashIterators.Select(v => v + address)]);

    internal sealed class Collector
    {
        private const string HashClass = "CQScanHash";

        private const string PartitionGroupClass = "CHashWorkfilePartitionGroup";

        private const string GetRowMethod = "GetRow";

        private const string LookupMethod = "Lookup";

        private const string InsertMethod = "Insert";

        private const string VTableName = "`vftable'";

        private List<ulong> GetRows { get; } = [];

        private List<ulong> Lookups { get; } = [];

        private List<ulong> Inserts { get; } = [];

        private List<ulong> VTables { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var methodName);

            if (className is HashClass && methodName is GetRowMethod)
            {
                GetRows.Add(rva);
            }
            else if (className is PartitionGroupClass && methodName is LookupMethod)
            {
                Lookups.Add(rva);
            }
            else if (className is PartitionGroupClass && methodName is InsertMethod)
            {
                Inserts.Add(rva);
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
            => GetRows.Count == 0 || Lookups.Count == 0
                ? Empty
                : new([.. GetRows.Distinct()], [.. Lookups.Distinct()], [.. Inserts.Distinct()], [.. VTables.Distinct()]);
    }
}
