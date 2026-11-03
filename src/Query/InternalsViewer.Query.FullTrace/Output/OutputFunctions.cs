using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Output;

internal static class OutputFunctions
{
    public const string ExpressionServiceClass = "CEsExec";

    public const string GeneralEvalMethod = "GeneralEval";

    public const string GeneralEval4Method = "GeneralEval4";

    public const string FastCompareMethod = "FastCompare";

    public const string FastCompare1Method = "FastCompare1";

    public static HashSet<string> EvaluationMethods { get; } = new(StringComparer.Ordinal)
    {
        FastCompareMethod,
        FastCompare1Method,
        GeneralEvalMethod,
        GeneralEval4Method
    };

    private static Dictionary<string, OutputFunctionType> Names { get; } = new(StringComparer.Ordinal)
    {
        [$"{ExpressionServiceClass}::{GeneralEvalMethod}"] = OutputFunctionType.Evaluation,
        [$"{ExpressionServiceClass}::{GeneralEval4Method}"] = OutputFunctionType.Evaluation,
        ["GetData"] = OutputFunctionType.ValueRead,
        ["GetDataWithBuffer"] = OutputFunctionType.ValueRead,
        ["GetDataFromXvar"] = OutputFunctionType.VariantRead,
        ["GetDataFromXvar8"] = OutputFunctionType.VariantRead,
        ["CValOdsRow::SetDataX"] = OutputFunctionType.Send
    };

    private static Dictionary<string, OutputValueClass> ValueClasses { get; } = new(StringComparer.Ordinal)
    {
        ["CValOdsRow"] = OutputValueClass.OutputRow,
        ["CValConstXVar"] = OutputValueClass.RowBuffer,
        ["CValSSTable"] = OutputValueClass.Table,
        ["CValHash"] = OutputValueClass.Hash,
        ["CValHashSwitch"] = OutputValueClass.HashSwitch,
        ["CValHashCachedSwitch"] = OutputValueClass.HashCachedSwitch,
        ["CValBatchRedirect"] = OutputValueClass.BatchRedirect,
        ["CValXVarTable"] = OutputValueClass.VariantTable
    };

    public static NamedFunctionCollector<OutputFunctionType> CreateCollector() => new(Names);

    public static AddressTable<OutputValueClass> GetValueClasses(IReadOnlyDictionary<ulong, string[]> vtables)
        => VtableResolver.GetFirstMatches(vtables, GetValueClass);

    private static OutputValueClass? GetValueClass(string className)
        => ValueClasses.TryGetValue(className, out var valueClass) ? valueClass : null;
}
