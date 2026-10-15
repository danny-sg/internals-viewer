using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed record RowBufferFunctions(RowBufferRead[] Reads, RowBufferLayout[] Layouts)
{
    public static RowBufferFunctions Empty { get; } = new([], []);

    public static RowBufferFunctions Merge(IEnumerable<RowBufferFunctions> functions)
    {
        var all = functions.ToList();

        return new([.. all.SelectMany(f => f.Reads)], [.. all.SelectMany(f => f.Layouts)]);
    }

    public RowBufferFunctions Rebase(ulong address)
        => new([.. Reads.Select(r => r with { Function = r.Function + address })],
               [.. Layouts.Select(l => l with { VTable = l.VTable + address })]);

    internal sealed class Collector
    {
        private const int InstanceRegister = 0;

        private const uint BufferOffset = 0x80;

        private const uint BindingsOffset = 0x88;

        private const string RowsetClass = "CQScanRowsetNew";

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

            RowBufferLayout[] layouts = [.. rowsets.Where(VTables.ContainsKey)
                                                   .Select(c => new RowBufferLayout(VTables[c], BufferOffset, BindingsOffset))];

            if (layouts.Length == 0)
            {
                return Empty;
            }

            HashSet<uint> functions = [.. Methods.Where(m => rowsets.Contains(m.ClassName)).Select(m => m.Rva), .. Helpers];

            return functions.Count == 0 ? Empty : new([.. functions.Select(f => new RowBufferRead(f, InstanceRegister))], layouts);
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
