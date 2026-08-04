namespace InternalsViewer.Execution.Interfaces.Iterators;

/// <summary>
/// An iterator that reads one input and passes its rows on
/// </summary>
/// <remarks>
/// The input is exposed for the same reason a join's two are - anything walking the running tree, to find what an operator settled on or
/// what it is holding, has to reach the operators below without knowing what stands between them.
/// </remarks>
public interface IUnaryIterator : IIterator
{
    IIterator? Input { get; }
}
