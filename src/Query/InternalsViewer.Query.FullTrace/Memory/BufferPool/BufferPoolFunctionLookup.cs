using InternalsViewer.Query.FullTrace.Calls.Tree;
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

    private static Dictionary<string, BufferPoolFunctionType>.AlternateLookup<ReadOnlySpan<char>> NameLookup { get; }
        = Names.GetAlternateLookup<ReadOnlySpan<char>>();

    public static BufferPoolFunctionType? GetFunctionType(ReadOnlySpan<char> name)
        => NameLookup.TryGetValue(name, out var type) ? type : null;

    public static bool IsBufferPool(CallFrame? frame)
        => frame is { Function.ClassName: BufferPoolClass } && frame.Module.Name.Equals(Module, StringComparison.OrdinalIgnoreCase);

    public static bool IsPostRead(CallFrame? frame)
        => frame is { Function: { ClassName: FileControlBlockClass, MethodName: PostReadMethod } }
           && frame.Module.Name.Equals(Module, StringComparison.OrdinalIgnoreCase);
}
