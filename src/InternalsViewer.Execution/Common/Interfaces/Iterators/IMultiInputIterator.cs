namespace InternalsViewer.Execution.Common.Interfaces.Iterators;

public interface IMultiInputIterator : IIterator
{
    IReadOnlyList<IIterator> Inputs { get; }
}
