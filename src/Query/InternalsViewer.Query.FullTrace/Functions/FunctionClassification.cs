using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Classification wrapper for a function
/// </summary>
/// <remarks>
/// Multiple symbols can resolve to the same RVA (for example identical functions folded together by the linker). When this happens the
/// classifications are combined with <see cref="Merge"/>, which resolves each flag conservatively:
///
///     - IsExcluded          - excluded only if every symbol at the address is excluded
///     - HasInstanceMethod   - set if any symbol at the address is an instance method
///     - InstanceClassesOnly - set only if every symbol at the address belongs to an instance class
///     - MemoryFunction      - kept only if every symbol at the address agrees, otherwise cleared
///
/// IsInstanceMethod is only true when an instance method is present and all symbols are instance classes. A folded address shared with a
/// non-instance class is never treated as an instance method.
/// </remarks>
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
