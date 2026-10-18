using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.PublishedEvents;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Accessors;
using InternalsViewer.Query.FullTrace.RowBuffers.HashRows;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantRows;
using InternalsViewer.Query.FullTrace.RowSources;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed record ReplayFunctions(ulong[] ExcludedFunctions,
                                       InstanceMethod[] InstanceMethods,
                                       LocatorRead[] LocatorReads,
                                       PublishedEventFunctions PublishedEvents,
                                       MemoryFunction[] MemoryFunctions,
                                       MemoryMapFunction[] MemoryMapFunctions,
                                       RowBufferFunctions RowBuffers,
                                       BatchFunctions Batches,
                                       HashRowFunctions HashRows,
                                       VariantRowFunctions VariantRows,
                                       RowSourceFunctions RowSources,
                                       AccessorFunctions Accessors,
                                       OutputFunctions Output)
{
    public static ReplayFunctions Empty { get; } = new([],
                                                       [],
                                                       [],
                                                       PublishedEventFunctions.Empty,
                                                       [],
                                                       [],
                                                       RowBufferFunctions.Empty,
                                                       BatchFunctions.Empty,
                                                       HashRowFunctions.Empty,
                                                       VariantRowFunctions.Empty,
                                                       RowSourceFunctions.Empty,
                                                       AccessorFunctions.Empty,
                                                       OutputFunctions.Empty);

    public static ReplayFunctions Merge(IReadOnlyList<ReplayFunctions> functions)
        => new([.. functions.SelectMany(f => f.ExcludedFunctions)],
               [.. functions.SelectMany(f => f.InstanceMethods)],
               [.. functions.SelectMany(f => f.LocatorReads)],
               PublishedEventFunctions.Merge(functions.Select(f => f.PublishedEvents)),
               [.. functions.SelectMany(f => f.MemoryFunctions)],
               [.. functions.SelectMany(f => f.MemoryMapFunctions)],
               RowBufferFunctions.Merge(functions.Select(f => f.RowBuffers)),
               BatchFunctions.Merge(functions.Select(f => f.Batches)),
               HashRowFunctions.Merge(functions.Select(f => f.HashRows)),
               VariantRowFunctions.Merge(functions.Select(f => f.VariantRows)),
               RowSourceFunctions.Merge(functions.Select(f => f.RowSources)),
               AccessorFunctions.Merge(functions.Select(f => f.Accessors)),
               OutputFunctions.Merge(functions.Select(f => f.Output)));

    public ReplayFunctions Rebase(ulong address)
        => new([.. ExcludedFunctions.Select(f => f + address)],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. LocatorReads.Select(l => l with { Function = l.Function + address })],
               PublishedEvents.Rebase(address),
               [.. MemoryFunctions.Select(f => f with { Address = f.Address + address })],
               [.. MemoryMapFunctions.Select(f => f with { Address = f.Address + address })],
               RowBuffers.Rebase(address),
               Batches.Rebase(address),
               HashRows.Rebase(address),
               VariantRows.Rebase(address),
               RowSources.Rebase(address),
               Accessors.Rebase(address),
               Output.Rebase(address));
}
