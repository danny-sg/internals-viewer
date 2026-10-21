using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal sealed record PlanNodeFunctions(IReadOnlyDictionary<ulong, PlanNodeFunctionType> Functions,
                                          IReadOnlyDictionary<ulong, PlanObjectClass> PlanObjects)
{
    public static PlanNodeFunctions Empty { get; } = new(new Dictionary<ulong, PlanNodeFunctionType>(),
                                                          new Dictionary<ulong, PlanObjectClass>());

    public static PlanNodeFunctions Merge(IEnumerable<PlanNodeFunctions> functions)
    {
        var all = functions.ToList();

        return new(AddressMap.Merge(all.Select(f => f.Functions)), AddressMap.Merge(all.Select(f => f.PlanObjects)));
    }

    public PlanNodeFunctions Rebase(ulong address) => new(AddressMap.Rebase(Functions, address), AddressMap.Rebase(PlanObjects, address));

    internal sealed class Collector
    {
        private const string VTableName = "`vftable'";

        private Dictionary<uint, PlanNodeFunctionType> Candidates { get; } = [];

        private HashSet<uint> Others { get; } = [];

        private Dictionary<ulong, PlanObjectClass> PlanObjects { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var methodName);

            if (IteratorSymbols.IsBatchIterator(className) && methodName is IteratorSymbols.GetBatchDataMethod)
            {
                Candidates.TryAdd(rva, PlanNodeFunctionType.BatchIterator);
            }
            else if (IteratorSymbols.IsBatchPlanObject(className) && methodName is IteratorSymbols.BatchFactoryMethod)
            {
                Candidates.TryAdd(rva, PlanNodeFunctionType.BatchFactory);
            }
            else if (className is IteratorSymbols.QueryScanClass && methodName is IteratorSymbols.TeardownMethod)
            {
                Candidates.TryAdd(rva, PlanNodeFunctionType.Teardown);
            }
            else if (!IteratorSymbols.IsRowIterator(className))
            {
                Others.Add(rva);
            }
            else if (methodName is IteratorSymbols.GetRowMethod or IteratorSymbols.GetRowOrReQualifyMethod or IteratorSymbols.OpenMethod)
            {
                var type = className is IteratorSymbols.BatchHelperClass ? PlanNodeFunctionType.BatchHelper : PlanNodeFunctionType.Iterator;

                Candidates.TryAdd(rva, type);
            }
            else if (methodName.SequenceEqual(className))
            {
                Candidates.TryAdd(rva, PlanNodeFunctionType.Constructor);
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

            if (IteratorSymbols.IsBatchPlanObject(className))
            {
                PlanObjects.TryAdd(rva, PlanObjectClass.Batch);
            }
            else if (IteratorSymbols.IsRowPlanObject(className))
            {
                PlanObjects.TryAdd(rva, PlanObjectClass.Row);
            }
        }

        public PlanNodeFunctions Build()
            => new(Candidates.Where(c => !Others.Contains(c.Key)).ToDictionary(c => (ulong)c.Key, c => c.Value), PlanObjects);
    }
}
