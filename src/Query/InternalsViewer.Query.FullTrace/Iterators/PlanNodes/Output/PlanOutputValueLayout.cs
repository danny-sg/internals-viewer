using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public static class PlanOutputValueLayout
{
    public const string CachedSwitchClass = "CValHashCachedSwitch";

    public const string StorageValueClass = "CValSSTable";

    public const string OutputListClass = "CBndOutputs";

    public const string ExpressionValueClass = "CValExpr";

    public const string ExecutorClass = "CEsExec";

    public const string ExecValueClass = "CValExec";

    public static IReadOnlyList<MemoryField> GetFields(MemoryHistoryStore store, string? className)
    {
        var layout = store.Layout;

        return className switch
        {
            TraceHashTableLayout.ValueClass => [layout.HashValue.RecordOffsetField, layout.HashValue.SlotField],
            StorageValueClass => [layout.StorageValue.SlotField, layout.StorageValue.AccessorField],
            CachedSwitchClass => [layout.CachedSwitch.ValueField, .. layout.CachedSwitch.EntryFields],
            ExpressionValueClass => [layout.ExpressionValue.ExecutorField],
            ExecutorClass => [layout.Executor.ValuesField],
            OutputListClass => layout.OutputList.Fields,
            AccessorBindingLayout.ClassName => AccessorBindingLayout.Fields,
            _ => []
        };
    }
}
