using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Helpers.Converters.Calls;
using Windows.UI;

namespace InternalsViewer.UI.App.Helpers;

public static class PlanOutputLinkText
{
    public const string RowBufferReference = "Row Buffer";

    public const string HashReference = "Hash";

    public const string ScalarReference = "Scalar";

    public const string RowsetReference = "Rowset";

    public const string BatchRedirectReference = "Batch Redirect";

    public const string VariablesReference = "Variables";

    public const string CounterReference = "Counter";

    private const string ReferencePrefix = "CValRef";

    private static readonly Dictionary<string, OperatorCategory> ReferenceCategories = new()
    {
        [RowBufferReference] = OperatorCategory.Modification,
        [HashReference] = OperatorCategory.Join,
        [ScalarReference] = OperatorCategory.Transformation,
        [RowsetReference] = OperatorCategory.DataAccess,
        [VariablesReference] = OperatorCategory.Transformation,
        [CounterReference] = OperatorCategory.Transformation
    };

    public static Color? GetReferenceColour(string reference)
        => ReferenceCategories.TryGetValue(reference, out var category) ? OperatorCategoryToBrushConverter.ColourFor(category) : null;

    public static string GetName(PlanOutputLinkType type) => type switch
    {
        PlanOutputLinkType.OutputList => "Output List",
        PlanOutputLinkType.OutputArray => "Output Array",
        PlanOutputLinkType.Reference => "Reference",
        PlanOutputLinkType.Expression => "Expression",
        PlanOutputLinkType.Producer => "Producer",
        PlanOutputLinkType.ExecutionContext => "Execution Context",
        PlanOutputLinkType.QueryContext => "Query Execution Context",
        PlanOutputLinkType.Iterator => "Iterator",
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
        PlanOutputLinkType.GroupColumns => "Group Columns",
        PlanOutputLinkType.AggregateColumns => "Aggregate Columns",
        PlanOutputLinkType.VariableTable => "Variable Table",
        PlanOutputLinkType.CountColumns => "Count Columns",
        PlanOutputLinkType.Variables => "Variables",
        PlanOutputLinkType.PlanObject => "Plan Object",
        PlanOutputLinkType.RowBindings => "Row Bindings",
        PlanOutputLinkType.ColumnBindings => "Column Bindings",
        PlanOutputLinkType.ColumnBinding => "Column Binding",
        PlanOutputLinkType.Executor => "Executor",
        PlanOutputLinkType.Executors => "Executors",
        _ => "Input Column"
    };

    public static string GetReferenceName(PlanOutputStage stage) => stage.ReferenceClass switch
    {
        "CValRefOrdinal" => stage.Type switch
        {
            PlanOutputStageType.Variables => VariablesReference,
            PlanOutputStageType.Counter => CounterReference,
            _ => RowBufferReference
        },
        "CValRefJoin" => HashReference,
        "CValRefCalculated" => ScalarReference,
        "CValRefBatchRedirect" => BatchRedirectReference,
        "CValRefSS" => RowsetReference,
        { } className when className.StartsWith(ReferencePrefix, StringComparison.Ordinal) => className[ReferencePrefix.Length..],
        { } className => className,
        null => string.Empty
    };

    public static string? GetObjectName(string? className) => className switch
    {
        "CValHashSwitch" => "Hash Switch",
        "CValHashCachedSwitch" => "Cached Switch",
        "CValHash" => "Hash Value",
        "CValSSTable" => "Rowset Value",
        "CValExpr" => "Expression",
        "CEsExec" => "Executor",
        "CValEsConstXVar" => "Constant",
        "CValXVarTable" => "Variable",
        "CValXVarTableRow" => "Variable Row",
        "CValRowNoHrow" => "Row",
        _ => null
    };

    public static string GetName(PlanOutputStageType type) => type switch
    {
        PlanOutputStageType.RowBuffer => "Row Buffer",
        PlanOutputStageType.RowContext => "Row Context",
        PlanOutputStageType.InputValue => "Input Value",
        PlanOutputStageType.Variables => "Variables",
        PlanOutputStageType.Counter => "Counter",
        PlanOutputStageType.Record => "Record",
        PlanOutputStageType.Expression => "Expression",
        _ => string.Empty
    };
}
