using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.DataAccess;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Row;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution.Iterators;

public sealed class IteratorFactory(IServiceProvider services) : IIteratorFactory
{
    public IIterator Create(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition
                => services.GetRequiredService<NestedLoopsIterator>(),
            MergeJoinDefinition
                => services.GetRequiredService<MergeJoinIterator>(),
            HashMatchDefinition
                => services.GetRequiredService<HashMatchIterator>(),
            TopDefinition
                => services.GetRequiredService<TopIterator>(),
            SelectDefinition
                => services.GetRequiredService<SelectIterator>(),
            AllocationScanDefinition
                => services.GetRequiredService<AllocationScanIterator>(),
            HeapFetchDefinition
                => services.GetRequiredService<HeapFetchIterator>(),
            RangeDefinition or SeekDefinition
                => services.GetRequiredService<IndexIterator>(),
            _ => throw new ArgumentException($"No iterator runs a {definition.GetType().Name}")
        };
}
