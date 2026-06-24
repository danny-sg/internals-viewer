using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Interfaces.Annotations;

public interface IDataStructure
{
    public List<DataStructureItem> MarkItems { get; }
}
