using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Interfaces.BatchMode;

namespace InternalsViewer.Execution.Interfaces;

/// <summary>
/// Builds the iterator that runs a definition
/// </summary>
/// <remarks>
/// An operator that reads another asks for its input through this rather than being handed one, which is what lets a tree be any depth.
/// </remarks>
public interface IIteratorFactory
{
    IIterator Create(IteratorDefinition definition);

    IBatchIterator CreateBatch(IteratorDefinition definition);
}
