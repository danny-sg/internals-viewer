using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Functions;

internal readonly record struct FunctionClassification(bool Excluded,
                                                       bool HasInstanceMethod,
                                                       bool InstanceClassesOnly,
                                                       bool IsEventWriter,
                                                       string? PublishEventName,
                                                       MemoryFunction? Memory)
{
    public bool IsInstanceMethod => HasInstanceMethod && InstanceClassesOnly;

    public FunctionClassification Merge(FunctionClassification other)
        => new(Excluded && other.Excluded,
               HasInstanceMethod || other.HasInstanceMethod,
               InstanceClassesOnly && other.InstanceClassesOnly,
               IsEventWriter && other.IsEventWriter,
               PublishEventName == other.PublishEventName ? PublishEventName : null,
               Memory == other.Memory ? Memory : null);
}
