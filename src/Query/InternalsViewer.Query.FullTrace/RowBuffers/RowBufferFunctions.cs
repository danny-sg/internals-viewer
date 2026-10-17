using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed record RowBufferFunctions(ulong[] Functions, IReadOnlyDictionary<ulong, RowBufferFormat> Formats)
{
    public static RowBufferFunctions Empty { get; } = new([], new Dictionary<ulong, RowBufferFormat>());

    public static RowBufferFunctions Merge(IEnumerable<RowBufferFunctions> functions)
    {
        var all = functions.ToList();

        return new([.. all.SelectMany(f => f.Functions)], AddressMap.Merge(all.Select(f => f.Formats)));
    }

    public RowBufferFunctions Rebase(ulong address)
        => new([.. Functions.Select(f => f + address)], AddressMap.Rebase(Formats, address));

    internal sealed class Collector
    {
        private const string RowsetClass = "CQScanRowsetNew";

        private const string BatchHelperClass = "CQScanBatchHelper";

        private const string VTableName = "`vftable'";

        private const string HierarchyName = "`RTTI Class Hierarchy Descriptor'";

        private const string HelperClass = "CQScanNew";

        private const string HelperMethod = "GetRowOrReQualifyHelper";

        private static readonly string[] RowMethods = ["GetRow", "GetRowDirectSelect"];

        public IReadOnlyDictionary<string, uint> Hierarchies => HierarchyDescriptors;

        private Dictionary<string, uint> HierarchyDescriptors { get; } = [];

        private Dictionary<string, uint> VTables { get; } = [];

        private List<(string ClassName, uint Rva)> Methods { get; } = [];

        private HashSet<uint> Helpers { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var methodName);

            if (className is HelperClass && methodName is HelperMethod)
            {
                Helpers.Add(rva);
            }
            else if (className.Length > 0 && IsRowMethod(methodName))
            {
                Methods.Add((className.ToString(), rva));
            }
        }

        public void VisitData(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var memberName);

            if (className.Length == 0)
            {
                return;
            }

            if (memberName is VTableName)
            {
                VTables.TryAdd(className.ToString(), rva);
            }
            else if (memberName is HierarchyName)
            {
                HierarchyDescriptors.TryAdd(className.ToString(), rva);
            }
        }

        public RowBufferFunctions Build(IReadOnlyDictionary<string, string[]> bases)
        {
            HashSet<string> rowsets = [.. bases.Where(b => b.Value.Contains(RowsetClass)).Select(b => b.Key)];

            HashSet<string> batchHelpers = [.. bases.Where(b => b.Value.Contains(BatchHelperClass)).Select(b => b.Key)];

            var formats = new Dictionary<ulong, RowBufferFormat>();

            foreach (var rowset in rowsets.Where(VTables.ContainsKey))
            {
                formats[VTables[rowset]] = RowBufferFormat.Bindings;
            }

            foreach (var batchHelper in batchHelpers.Where(VTables.ContainsKey))
            {
                formats[VTables[batchHelper]] = RowBufferFormat.Batch;
            }

            if (formats.Count == 0)
            {
                return Empty;
            }

            HashSet<uint> functions =
            [
                .. Methods.Where(m => rowsets.Contains(m.ClassName) || batchHelpers.Contains(m.ClassName)).Select(m => m.Rva),
                .. Helpers
            ];

            return functions.Count == 0 ? Empty : new([.. functions.Select(f => (ulong)f)], formats);
        }

        private static bool IsRowMethod(ReadOnlySpan<char> name)
        {
            foreach (var method in RowMethods)
            {
                if (name.SequenceEqual(method))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
