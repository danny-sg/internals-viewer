using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal static class PlanNodeFunctionLookup
{
    public static AddressTable<PlanObjectClass> GetPlanObjects(IReadOnlyDictionary<ulong, string[]> vtables)
        => VtableResolver.GetFirstMatches(vtables, GetPlanObjectClass);

    public static PlanNodeFunctionType? GetFunctionType(ReadOnlySpan<char> name)
    {
        SymbolsHelper.Split(name, out var className, out var methodName);

        if (IteratorSymbolLookup.IsBatchIterator(className) && methodName is IteratorSymbolLookup.GetBatchDataMethod)
        {
            return PlanNodeFunctionType.BatchIterator;
        }

        if (IteratorSymbolLookup.IsBatchPlanObject(className) && methodName is IteratorSymbolLookup.BatchFactoryMethod)
        {
            return PlanNodeFunctionType.BatchFactory;
        }

        if (className is IteratorSymbolLookup.QueryScanClass && methodName is IteratorSymbolLookup.TeardownMethod)
        {
            return PlanNodeFunctionType.Teardown;
        }

        if (!IteratorSymbolLookup.IsRowIterator(className))
        {
            return null;
        }

        if (methodName is IteratorSymbolLookup.GetRowMethod
                          or IteratorSymbolLookup.GetRowOrReQualifyMethod
                          or IteratorSymbolLookup.OpenMethod
                          or IteratorSymbolLookup.OpenHelperMethod)
        {
            return className is IteratorSymbolLookup.BatchHelperClass ? PlanNodeFunctionType.BatchHelper : PlanNodeFunctionType.Iterator;
        }

        return methodName.SequenceEqual(className) ? PlanNodeFunctionType.Constructor : null;
    }

    private static PlanObjectClass? GetPlanObjectClass(string className)
    {
        if (IteratorSymbolLookup.IsBatchPlanObject(className))
        {
            return PlanObjectClass.Batch;
        }

        return IteratorSymbolLookup.IsRowPlanObject(className) ? PlanObjectClass.Row : null;
    }
}
