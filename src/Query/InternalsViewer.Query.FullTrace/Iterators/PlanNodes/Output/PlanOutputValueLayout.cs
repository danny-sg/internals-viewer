using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public static class PlanOutputValueLayout
{
    public const string CachedSwitchClass = "CValHashCachedSwitch";

    public const string StorageValueClass = "CValSSTable";

    public const string OutputListClass = "CBndOutputs";

    public static IReadOnlyList<MemoryField> GetFields(string? className) => className switch
    {
        TraceHashTableLayout.ValueClass => [EngineLayout.HashValue.RecordOffsetField, EngineLayout.HashValue.SlotField],
        StorageValueClass => [EngineLayout.StorageValue.SlotField, EngineLayout.StorageValue.AccessorField],
        CachedSwitchClass => [EngineLayout.CachedSwitch.ValueField],
        OutputListClass => EngineLayout.OutputList.Fields,
        AccessorBindingLayout.ClassName => AccessorBindingLayout.Fields,
        _ => []
    };
}
