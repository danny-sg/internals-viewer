using System.Globalization;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.CallStack.Arguments;

/// <summary>
/// Maps raw ulong values to a display value 
/// </summary>
/// <remarks>
/// Based on type, or a recognized enum
/// </remarks>
public static class ArgumentValue
{
    private const ulong HexThreshold = 0x10000;

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>> KnownEnums = new(StringComparer.Ordinal)
    {
        ["LatchBase::LATCH_TYPE"] = new(GetEnumNames<LatchMode>),
        ["LatchBase::LatchClass"] = new(GetEnumNames<LatchClass>),
        ["LCK_MODE"] = new(GetEnumNames<LockMode>),
        ["LockRes::LockResourceType"] = new(GetEnumNames<LockResourceType>),
        ["LockOwnerType"] = new(GetEnumNames<LockOwnerType>),
        ["PageHeader::PageType"] = new(GetEnumNames<PageType>),
        ["MDObjectType"] = new(GetEnumNames<ObjectType>),
        ["XeSqlPkg::FileIoMode"] = new(GetEnumNames<ReadMode>),
        ["EDisable_Rowgroup_For_FPGA"] = new(GetEnumNames<DisableRowGroupReason>),
        ["ColumnStoreObjectType"] = new(GetEnumNames<ColumnStoreObjectType>),
        ["RbpCPUInstructionSet"] = new(GetEnumNames<ColumnStoreInstructionSet>),
        ["RbpDataType"] = new(GetEnumNames<ColumnStoreDataType>),
        ["RbpEarlyFilterType"] = new(GetEnumNames<ColumnStoreEarlyFilterType>),
        ["RbpEncodingType"] = new(GetEnumNames<ColumnStoreEncodingType>),
        ["RbpFilterType"] = new(GetEnumNames<ColumnStoreFilterType>)
    };

    public static bool IsFloating(string type) => Normalise(type) is "float" or "double" or "long double";

    public static bool IsPointer(string type) => type.TrimEnd() is { Length: > 0 } trimmed && trimmed[^1] is '*' or '&';

    public static string Format(string type, ulong raw)
    {
        if (type == "this" || IsPointer(type))
        {
            return raw == 0 ? "nullptr" : $"0x{raw:X}";
        }

        var normalised = Normalise(type);

        if (normalised.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal))
        {
            return KnownEnums.TryGetValue(normalised[FunctionSignature.EnumPrefix.Length..], out var names)
                   && names.Value.TryGetValue((int)raw, out var name)
                ? name
                : ToNumber((int)raw);
        }

        return normalised switch
        {
            "bool" => (raw & 0xFF) != 0 ? "true" : "false",
            "char" or "signed char" or "__int8" => ToNumber((sbyte)raw),
            "unsigned char" or "unsigned __int8" => ToNumber((byte)raw),
            "short" or "__int16" => ToNumber((short)raw),
            "unsigned short" or "unsigned __int16" or "wchar_t" => ToNumber((ushort)raw),
            "int" or "long" or "__int32" or "enum" => ToNumber((int)raw),
            "unsigned int" or "unsigned long" or "unsigned __int32" => ToNumber((uint)raw),
            "__int64" or "long long" => ToNumber((long)raw),
            "unsigned __int64" or "unsigned long long" => ToNumber(raw),
            "float" => BitConverter.Int32BitsToSingle((int)raw).ToString(CultureInfo.InvariantCulture),
            "double" or "long double" => BitConverter.Int64BitsToDouble((long)raw).ToString(CultureInfo.InvariantCulture),
            _ => $"0x{raw:X}"
        };
    }

    private static Dictionary<int, string> GetEnumNames<T>() where T : struct, Enum
        => Enum.GetValues<T>()
               .DistinctBy(v => Convert.ToInt32(v, CultureInfo.InvariantCulture))
               .ToDictionary(v => Convert.ToInt32(v, CultureInfo.InvariantCulture), v => v.ToString());

    private static string ToNumber(long value)
        => value is > -(long)HexThreshold and < (long)HexThreshold
            ? value.ToString(CultureInfo.InvariantCulture)
            : $"{value.ToString(CultureInfo.InvariantCulture)} (0x{value:X})";

    private static string ToNumber(ulong value)
        => value < HexThreshold
            ? value.ToString(CultureInfo.InvariantCulture)
            : $"{value.ToString(CultureInfo.InvariantCulture)} (0x{value:X})";

    private static string Normalise(string type)
        => string.Join(' ', type.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .Where(part => part is not ("const" or "volatile" or "__ptr64")));
}
