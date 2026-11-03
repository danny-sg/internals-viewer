using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal static class PlanNodeFunctionLookup
{
    public static AddressTable<PlanObjectClass> GetPlanObjects(IReadOnlyDictionary<ulong, string[]> vtables)
        => VtableResolver.GetFirstMatches(vtables, GetPlanObjectClass);

    private static PlanObjectClass? GetPlanObjectClass(string className)
    {
        if (IteratorSymbolLookup.IsBatchPlanObject(className))
        {
            return PlanObjectClass.Batch;
        }

        return IteratorSymbolLookup.IsRowPlanObject(className) ? PlanObjectClass.Row : null;
    }

    internal sealed class Collector
    {
        private Dictionary<uint, PlanNodeFunctionType> Candidates { get; } = [];

        private HashSet<uint> Others { get; } = [];

        public void VisitFunction(uint rva, ReadOnlySpan<char> name)
        {
            SymbolsHelper.Split(name, out var className, out var methodName);

            if (IteratorSymbolLookup.IsBatchIterator(className) && methodName is IteratorSymbolLookup.GetBatchDataMethod)
            {
                Candidates.TryAdd(rva, PlanNodeFunctionType.BatchIterator);
            }
            else if (IteratorSymbolLookup.IsBatchPlanObject(className) && methodName is IteratorSymbolLookup.BatchFactoryMethod)
            {
                Candidates.TryAdd(rva, PlanNodeFunctionType.BatchFactory);
            }
            else if (className is IteratorSymbolLookup.QueryScanClass && methodName is IteratorSymbolLookup.TeardownMethod)
            {
                Candidates.TryAdd(rva, PlanNodeFunctionType.Teardown);
            }
            else if (!IteratorSymbolLookup.IsRowIterator(className))
            {
                Others.Add(rva);
            }
            else if (methodName is IteratorSymbolLookup.GetRowMethod
                                   or IteratorSymbolLookup.GetRowOrReQualifyMethod
                                   or IteratorSymbolLookup.OpenMethod
                                   or IteratorSymbolLookup.OpenHelperMethod)
            {
                var type = className is IteratorSymbolLookup.BatchHelperClass ? PlanNodeFunctionType.BatchHelper : PlanNodeFunctionType.Iterator;

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

        public AddressTable<PlanNodeFunctionType> Build()
            => new(Candidates.Where(c => !Others.Contains(c.Key)).ToDictionary(c => (ulong)c.Key, c => c.Value));
    }
}
