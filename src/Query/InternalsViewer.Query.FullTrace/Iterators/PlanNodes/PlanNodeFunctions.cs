using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal sealed record PlanNodeFunctions(IReadOnlyDictionary<ulong, PlanNodeFunctionType> Functions,
                                          IReadOnlyDictionary<ulong, PlanObjectClass> PlanObjects)
{
    public static PlanNodeFunctions Empty { get; } = new(new Dictionary<ulong, PlanNodeFunctionType>(),
                                                          new Dictionary<ulong, PlanObjectClass>());

    public static PlanNodeFunctions Merge(IEnumerable<PlanNodeFunctions> functions)
    {
        var all = functions.ToList();

        return new(AddressMapHelper.Merge(all.Select(f => f.Functions)), AddressMapHelper.Merge(all.Select(f => f.PlanObjects)));
    }

    public PlanNodeFunctions Rebase(ulong address) => new(AddressMapHelper.Rebase(Functions, address), AddressMapHelper.Rebase(PlanObjects, address));

    internal sealed class Collector
    {
        private Dictionary<uint, PlanNodeFunctionType> Candidates { get; } = [];

        private HashSet<uint> Others { get; } = [];

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

        public PlanNodeFunctions Build(IReadOnlyDictionary<ulong, string[]> vtables)
            => new(Candidates.Where(c => !Others.Contains(c.Key)).ToDictionary(c => (ulong)c.Key, c => c.Value),
                   VtableResolver.GetFirstMatches(vtables, GetPlanObjectClass));

        private static PlanObjectClass? GetPlanObjectClass(string className)
        {
            if (IteratorSymbols.IsBatchPlanObject(className))
            {
                return PlanObjectClass.Batch;
            }

            return IteratorSymbols.IsRowPlanObject(className) ? PlanObjectClass.Row : null;
        }
    }
}
