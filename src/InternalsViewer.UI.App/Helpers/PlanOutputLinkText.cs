using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;

namespace InternalsViewer.UI.App.Helpers;

public static class PlanOutputLinkText
{
    public static string GetName(PlanOutputLinkType type) => type switch
    {
        PlanOutputLinkType.OutputList => "Output List",
        PlanOutputLinkType.OutputArray => "Output Array",
        PlanOutputLinkType.Reference => "Reference",
        PlanOutputLinkType.Expression => "Expression",
        PlanOutputLinkType.Producer => "Producer",
        PlanOutputLinkType.ExecutionContext => "Execution Context",
        PlanOutputLinkType.QueryContext => "Query Execution Context",
        PlanOutputLinkType.IteratorMap => "Iterator Map",
        PlanOutputLinkType.MapEntry => "Map Entry",
        PlanOutputLinkType.Iterator => "Iterator",
        PlanOutputLinkType.Inputs => "Inputs",
        PlanOutputLinkType.ProfilingIterator => "Profiling Iterator",
        PlanOutputLinkType.OutputValues => "Output Values",
        PlanOutputLinkType.InputValues => "Input Values",
        PlanOutputLinkType.InputValue => "Input Value",
        PlanOutputLinkType.Value => "Value",
        PlanOutputLinkType.Reader => "Reader",
        PlanOutputLinkType.QueryScan => "Query Scan",
        PlanOutputLinkType.QueryScanContext => "Query Scan Context",
        PlanOutputLinkType.Statement => "Statement",
        PlanOutputLinkType.RowContext => "Row Context",
        PlanOutputLinkType.RowSlot => "Row Slot",
        PlanOutputLinkType.Rowset => "Rowset",
        PlanOutputLinkType.Row => "Row",
        PlanOutputLinkType.Record => "Record",
        PlanOutputLinkType.RowBuffer => "Row Buffer",
        PlanOutputLinkType.BufferEntry => "Buffer Entry",
        PlanOutputLinkType.Batch => "Batch",
        PlanOutputLinkType.Cursor => "Cursor",
        PlanOutputLinkType.ColumnMap => "Column Map",
        PlanOutputLinkType.VectorId => "Vector",
        PlanOutputLinkType.Selection => "Selection",
        PlanOutputLinkType.SelectedRow => "Row",
        PlanOutputLinkType.Vectors => "Vectors",
        PlanOutputLinkType.Vector => "Vector",
        PlanOutputLinkType.VectorSlot => "Row",
        _ => "Input Column"
    };

    public static string GetRowText(int offset) => offset < 0 ? $"Row Variable {-offset}" : $"Row 0x{offset:X}";
}
