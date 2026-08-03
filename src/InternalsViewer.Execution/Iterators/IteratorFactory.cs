using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.Allocations;
using InternalsViewer.Execution.Iterators.Heaps;
using InternalsViewer.Execution.Iterators.Indexes;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Row;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Execution.Iterators;

/// <summary>
/// Resolves the iterator each definition needs, giving it the plan's identity for the steps it will produce
/// </summary>
public sealed class IteratorFactory(IServiceProvider services) : IIteratorFactory
{
    public IStepIterator Create(IteratorDefinition definition)
    {
        var iterator = Resolve(definition);

        iterator.IteratorId = definition.NodeId;

        return iterator;
    }

    private IStepIterator Resolve(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition
                => services.GetRequiredService<NestedLoopsStepIterator>(),
            MergeJoinDefinition
                => services.GetRequiredService<MergeJoinStepIterator>(),
            HashMatchDefinition
                => services.GetRequiredService<HashMatchStepIterator>(),
            TopDefinition
                => services.GetRequiredService<TopIterator>(),
            AllocationScanDefinition
                => services.GetRequiredService<AllocationStepIterator>(),
            HeapFetchDefinition
                => services.GetRequiredService<HeapFetchStepIterator>(),
            RangeDefinition or SeekDefinition
                => services.GetRequiredService<IndexStepIterator>(),
            _ => throw new ArgumentException($"No iterator runs a {definition.GetType().Name}")
        };
}
