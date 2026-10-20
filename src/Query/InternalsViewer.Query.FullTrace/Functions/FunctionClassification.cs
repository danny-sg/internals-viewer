using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Functions;

internal readonly record struct FunctionClassification(bool IsExcluded,
                                                       bool HasInstanceMethod,
                                                       bool InstanceClassesOnly,
                                                       MemoryFunction? MemoryFunction)
{
    public bool IsInstanceMethod => HasInstanceMethod && InstanceClassesOnly;

    public FunctionClassification Merge(FunctionClassification other)
        => new(IsExcluded && other.IsExcluded,
               HasInstanceMethod || other.HasInstanceMethod,
               InstanceClassesOnly && other.InstanceClassesOnly,
               MemoryFunction == other.MemoryFunction ? MemoryFunction : null);
}
