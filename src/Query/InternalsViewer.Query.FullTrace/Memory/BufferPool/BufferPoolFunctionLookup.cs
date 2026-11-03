using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

internal static class BufferPoolFunctionLookup
{
    public const string Module = "sqlmin";

    public const string BufferPoolClass = "BPool";

    public const string GetMethod = "Get";

    public const string ReadAheadMethod = "ReadAhead";

    public const string FileControlBlockClass = "FCB";

    public const string PostReadMethod = "PostRead";

    private static Dictionary<string, BufferPoolFunctionType> Names { get; } = new(StringComparer.Ordinal)
    {
        [$"{BufferPoolClass}::{GetMethod}"] = BufferPoolFunctionType.Get,
        [$"{FileControlBlockClass}::{PostReadMethod}"] = BufferPoolFunctionType.PostRead
    };

    public static NamedFunctionCollector<BufferPoolFunctionType> CreateCollector() => new(Names);

    public static bool IsBufferPool(CallstackFrame? frame)
        => frame is { Resolved.ClassName: BufferPoolClass } && frame.Module.Equals(Module, StringComparison.OrdinalIgnoreCase);
}
