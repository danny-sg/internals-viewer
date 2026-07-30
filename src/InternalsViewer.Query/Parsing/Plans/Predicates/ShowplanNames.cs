namespace InternalsViewer.Query.Parsing.Plans.Predicates;

/// <summary>
/// Element and attribute values used by showplan XML predicates
/// </summary>
/// <remarks>
/// Showplan expresses predicates as a ScalarOperator tree. The names here mirror the schema so the
/// parser reads against the schema rather than against magic strings.
/// </remarks>
internal static class ShowplanNames
{
    public const string ScalarOperator = "ScalarOperator";
    public const string Compare = "Compare";
    public const string Logical = "Logical";
    public const string Const = "Const";
    public const string Identifier = "Identifier";
    public const string ColumnReference = "ColumnReference";
    public const string Intrinsic = "Intrinsic";
    public const string Arithmetic = "Arithmetic";
    public const string If = "IF";
    public const string Condition = "Condition";
    public const string Then = "Then";
    public const string Else = "Else";
    public const string Aggregate = "Aggregate";
    public const string Convert = "Convert";
    public const string ParameterList = "ParameterList";
    public const string ScalarExpressionList = "ScalarExpressionList";
    public const string SeekPredicates = "SeekPredicates";
    public const string SeekPredicateNew = "SeekPredicateNew";
    public const string SeekPredicate = "SeekPredicate";
    public const string SeekPredicatePart = "SeekPredicatePart";
    public const string SeekKeys = "SeekKeys";
    public const string Prefix = "Prefix";
    public const string StartRange = "StartRange";
    public const string EndRange = "EndRange";
    public const string RangeColumns = "RangeColumns";
    public const string RangeExpressions = "RangeExpressions";
    public const string Predicate = "Predicate";

    public const string CompareOp = "CompareOp";
    public const string Operation = "Operation";
    public const string ConstValue = "ConstValue";
    public const string ParameterCompiledValue = "ParameterCompiledValue";
    public const string ParameterRuntimeValue = "ParameterRuntimeValue";
    public const string Implicit = "Implicit";
    public const string ScanType = "ScanType";
    public const string Column = "Column";
    public const string FunctionName = "FunctionName";
    public const string AggType = "AggType";
    public const string Distinct = "Distinct";
}
