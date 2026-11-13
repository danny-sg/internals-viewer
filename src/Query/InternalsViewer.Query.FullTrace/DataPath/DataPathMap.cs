using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.DataPath;

public sealed class DataPathMap
{
    public static DataPathMap Empty { get; } = new();

    public PlanNode? Statement { get; init; }

    public IReadOnlyList<DataPathField> Columns { get; init; } = [];

    public IReadOnlyList<DataPathField> Roots { get; init; } = [];

    public IReadOnlyList<DataPathField> Fields { get; init; } = [];

    public IReadOnlyDictionary<int, PlanOutputRoute> Routes { get; init; } = new Dictionary<int, PlanOutputRoute>();

    internal IReadOnlyDictionary<(int NodeId, int Index), DataPathField> Outputs { get; init; }
        = new Dictionary<(int NodeId, int Index), DataPathField>();

    internal IReadOnlyDictionary<int, int> Levels { get; init; } = new Dictionary<int, int>();

    public DataPathField? GetOutput(int nodeId, int index) => Outputs.GetValueOrDefault((nodeId, index));

    public int? GetLevel(int nodeId) => Levels.TryGetValue(nodeId, out var level) ? level : null;
}
