using InternalsViewer.Query.FullTrace.Evaluations;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.RowSources;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed record ReplayFunctions(ulong[] ExcludedFunctions,
                                       InstanceMethod[] InstanceMethods,
                                       LocatorRead[] LocatorReads,
                                       MemoryFunction[] MemoryFunctions,
                                       MemoryMapFunction[] MemoryMapFunctions,
                                       RowSourceFunctions RowSources,
                                       OutputFunctions Output,
                                       EvaluationFunctions Evaluations)
{
    public static ReplayFunctions Empty { get; } = new([],
                                                       [],
                                                       [],
                                                       [],
                                                       [],
                                                       RowSourceFunctions.Empty,
                                                       OutputFunctions.Empty,
                                                       EvaluationFunctions.Empty);

    public static ReplayFunctions Merge(IReadOnlyList<ReplayFunctions> functions)
        => new([.. functions.SelectMany(f => f.ExcludedFunctions)],
               [.. functions.SelectMany(f => f.InstanceMethods)],
               [.. functions.SelectMany(f => f.LocatorReads)],
               [.. functions.SelectMany(f => f.MemoryFunctions)],
               [.. functions.SelectMany(f => f.MemoryMapFunctions)],
               RowSourceFunctions.Merge(functions.Select(f => f.RowSources)),
               OutputFunctions.Merge(functions.Select(f => f.Output)),
               EvaluationFunctions.Merge(functions.Select(f => f.Evaluations)));

    public ReplayFunctions Rebase(ulong address)
        => new([.. ExcludedFunctions.Select(f => f + address)],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. LocatorReads.Select(l => l with { Function = l.Function + address })],
               [.. MemoryFunctions.Select(f => f with { Address = f.Address + address })],
               [.. MemoryMapFunctions.Select(f => f with { Address = f.Address + address })],
               RowSources.Rebase(address),
               Output.Rebase(address),
               Evaluations.Rebase(address));
}
