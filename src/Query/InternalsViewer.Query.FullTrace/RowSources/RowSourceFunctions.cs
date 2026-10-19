using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.RowSources.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.RowSources;

internal sealed record RowSourceFunctions(IReadOnlyDictionary<ulong, RowSourceFunctionType> Functions,
                                          IReadOnlyDictionary<ulong, PlanObjectClass> PlanObjects)
{
    public static RowSourceFunctions Empty { get; } = new(new Dictionary<ulong, RowSourceFunctionType>(),
                                                          new Dictionary<ulong, PlanObjectClass>());

    public static RowSourceFunctions Merge(IEnumerable<RowSourceFunctions> functions)
    {
        var all = functions.ToList();

        return new(AddressMap.Merge(all.Select(f => f.Functions)), AddressMap.Merge(all.Select(f => f.PlanObjects)));
    }

    public RowSourceFunctions Rebase(ulong address) => new(AddressMap.Rebase(Functions, address), AddressMap.Rebase(PlanObjects, address));

    internal sealed class Collector
    {
        private const string IteratorPrefix = "CQScan";

        private const string BatchIteratorPrefix = "CBpQScan";

        private const string BatchIteratorMethod = "BpGetBatchData";

        private const string BatchFactoryMethod = "PbpqsGet";

        private const string CreatorPrefix = "CreateVal";

        private const string PlanObjectPrefix = "CXte";

        private const string BatchPlanObjectPrefix = "CBpXte";

        private const string VTableName = "`vftable'";

        private const string TeardownClass = "CQueryScan";

        private const string TeardownMethod = "DestroyQuery";

        private Dictionary<uint, RowSourceFunctionType> Candidates { get; } = [];

        private HashSet<uint> Others { get; } = [];

        private Dictionary<ulong, PlanObjectClass> PlanObjects { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var methodName);

            if (className.StartsWith(BatchIteratorPrefix, StringComparison.Ordinal) && methodName is BatchIteratorMethod)
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.BatchIterator);
            }
            else if (className.StartsWith(BatchPlanObjectPrefix, StringComparison.Ordinal) && methodName is BatchFactoryMethod)
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.BatchFactory);
            }
            else if (className is TeardownClass && methodName is TeardownMethod)
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.Teardown);
            }
            else if (!className.StartsWith(IteratorPrefix, StringComparison.Ordinal))
            {
                Others.Add(rva);
            }
            else if (methodName is "GetRow" or "GetRowOrReQualifyHelper" or "Open")
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.Iterator);
            }
            else if (methodName.StartsWith(CreatorPrefix, StringComparison.Ordinal))
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.Creator);
            }
            else if (methodName.SequenceEqual(className))
            {
                Candidates.TryAdd(rva, RowSourceFunctionType.Constructor);
            }
            else
            {
                Others.Add(rva);
            }
        }

        public void VisitData(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var memberName);

            if (memberName is not VTableName)
            {
                return;
            }

            if (className.StartsWith(BatchPlanObjectPrefix, StringComparison.Ordinal))
            {
                PlanObjects.TryAdd(rva, PlanObjectClass.Batch);
            }
            else if (className.StartsWith(PlanObjectPrefix, StringComparison.Ordinal))
            {
                PlanObjects.TryAdd(rva, PlanObjectClass.Row);
            }
        }

        public RowSourceFunctions Build()
            => new(Candidates.Where(c => !Others.Contains(c.Key)).ToDictionary(c => (ulong)c.Key, c => c.Value), PlanObjects);
    }
}
