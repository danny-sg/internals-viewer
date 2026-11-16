using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

public static class MemoryFieldDecoderLookup
{
    private const string NoneText = "None";

    private const string VariablePrefix = "Variable ";

    private static readonly (ColumnBindingFlags Flag, string Name)[] FlagNames =
    [
        (ColumnBindingFlags.Length, "Length"),
        (ColumnBindingFlags.ByReference, "By Reference"),
        (ColumnBindingFlags.LargeObject, "Large Object")
    ];

    public static string GetText(MemoryFieldDecoder decoder, ulong value) => decoder switch
    {
        MemoryFieldDecoder.Latch => GetLatchText(value),
        MemoryFieldDecoder.SqlType => SqlTypeHelpers.TryGetSqlType((byte)value, out var sqlType) ? $"{sqlType}" : $"0x{value:X2}",
        MemoryFieldDecoder.BindingFlags => GetFlagsText((ColumnBindingFlags)(uint)value),
        MemoryFieldDecoder.RecordOffset => (int)(uint)value is < 0 and var offset ? $"{VariablePrefix}{-offset}" : $"{value}",
        _ => $"{value}"
    };

    private static string GetLatchText(ulong state)
    {
        var shared = BufferDescriptor.GetSharedCount(state);

        var mode = BufferDescriptor.IsExclusive(state)
            ? "Exclusive"
            : shared > 0
                ? $"Shared, {shared} {(shared == 1 ? "Holder" : "Holders")}"
                : "Released";

        return BufferDescriptor.HasWaiters(state) ? $"{mode}, Waiters" : mode;
    }

    private static string GetFlagsText(ColumnBindingFlags flags)
        => FlagNames.Where(f => (flags & f.Flag) != 0).Select(f => f.Name).ToList() is { Count: > 0 } names
            ? string.Join(", ", names)
            : NoneText;
}
