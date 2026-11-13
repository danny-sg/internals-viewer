using InternalsViewer.Query.FullTrace.DataPath.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.DataPath;

public sealed class DataPathField(PlanNode node, ColumnReference column)
{
    public PlanNode Node { get; } = node;

    public ColumnReference Column { get; } = column;

    public string Name => Column.Column;

    public PlanOutputColumn? Output { get; internal set; }

    public SqlTypeInfo? Type { get; internal set; }

    public DataPathFieldKind Kind { get; internal set; }

    public ColumnUseType Uses { get; internal set; }

    public DefinedValueInfo? Definition { get; internal set; }

    public PlanNode? Expression { get; internal set; }

    public PlanNode? Evaluator { get; internal set; }

    public PlanOutputStage? Stage { get; internal set; }

    public PlanOutputRouteNode? Held { get; internal set; }

    public (TracePosition From, TracePosition To)? Scope { get; internal set; }

    public List<DataPathLink> Inputs { get; } = [];
}
