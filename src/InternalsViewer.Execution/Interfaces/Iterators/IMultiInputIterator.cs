namespace InternalsViewer.Execution.Interfaces.Iterators;

public interface IMultiInputIterator : IIterator
{
    IReadOnlyList<IIterator> Inputs { get; }
}
