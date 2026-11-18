using System.Numerics;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace.Locators;
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

    public static string GetText(EngineLayout layout, MemoryFieldDecoder decoder, ulong value) => decoder switch
    {
        MemoryFieldDecoder.Latch => GetLatchText(layout.Buffer, value),
        MemoryFieldDecoder.SqlType => SqlTypeHelpers.TryGetSqlType((byte)value, out var sqlType) ? $"{sqlType}" : $"0x{value:X2}",
        MemoryFieldDecoder.BindingFlags => GetFlagsText((ColumnBindingFlags)(uint)value),
        MemoryFieldDecoder.RecordOffset => (int)(uint)value is < 0 and var offset ? $"{VariablePrefix}{-offset}" : $"{value}",
        MemoryFieldDecoder.Bitmap => GetBitmapText(value),
        MemoryFieldDecoder.TypeInfo => SqlTypeInfo.Create(value, 0).ToString(),
        MemoryFieldDecoder.TypeDetails => GetTypeDetailsText(SqlTypeInfo.Create(0, value)),
        _ => $"{value}"
    };

    private static string GetBitmapText(ulong value)
    {
        var count = Math.Max(1, (sizeof(ulong) * 8 - BitOperations.LeadingZeroCount(value) + 7) / 8);

        return string.Join(" ", BitConverter.GetBytes(value).Take(count).Select(b => b.ToBinaryString()));
    }

    private static string GetTypeDetailsText(SqlTypeInfo details)
    {
        var text = $"Maximum Length {details.MaxLength}, Precision {details.Precision}, Scale {details.Scale}";

        return details.Collation == 0 ? text : $"{text}, Collation {details.Collation}";
    }

    private static string GetLatchText(EngineLayout.BufferLayout layout, ulong state)
    {
        var shared = BufferDescriptor.GetSharedCount(layout, state);

        var mode = BufferDescriptor.IsExclusive(layout, state)
            ? "Exclusive"
            : shared > 0
                ? $"Shared, {shared} {(shared == 1 ? "Holder" : "Holders")}"
                : "Released";

        return BufferDescriptor.HasWaiters(layout, state) ? $"{mode}, Waiters" : mode;
    }

    private static string GetFlagsText(ColumnBindingFlags flags)
        => FlagNames.Where(f => (flags & f.Flag) != 0).Select(f => f.Name).ToList() is { Count: > 0 } names
            ? string.Join(", ", names)
            : NoneText;
}
