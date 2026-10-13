using System.Globalization;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Indexes.Enums;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.FullTrace.Arguments;

/// <summary>
/// Maps raw ulong values to a display value 
/// </summary>
/// <remarks>
/// Based on type, or a recognized enum
/// </remarks>
public static class ArgumentValue
{
    private const ulong HexThreshold = 0x10000;

    private const int HoBtIdShift = 16;

    private const string HoBtIdType = "HoBtId";

    private const int MaximumStackType = 256;

    private const string FlagSeparator = " | ";

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>> KnownEnums = new(StringComparer.Ordinal)
    {
        ["LatchBase::LATCH_TYPE"] = new(GetEnumNames<LatchMode>),
        ["LatchBase::LatchClass"] = new(GetEnumNames<LatchClass>),
        ["BTreeMgr::VR_STATUS"] = new(GetEnumNames<BTreeRootStatus>),
        ["LCK_MODE"] = new(GetEnumNames<LockMode>),
        ["LCK_RESULT"] = new(GetEnumNames<LockResult>),
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

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>> KnownFlags = new(StringComparer.Ordinal)
    {
        ["BPool::StealOption"] = new(GetEnumNames<StealOptions>)
    };

    private static readonly HashSet<string> ReadableTypes = new(StringComparer.Ordinal)
    {
        "bool", "short", "__int16", "unsigned short", "unsigned __int16", "int", "long", "__int32", "unsigned int", "unsigned long",
        "unsigned __int32", "__int64", "long long", "unsigned __int64", "unsigned long long", "float", "double", HoBtIdType
    };

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>>.AlternateLookup<ReadOnlySpan<char>> KnownEnumLookup =
        KnownEnums.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>>.AlternateLookup<ReadOnlySpan<char>> KnownFlagLookup =
        KnownFlags.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> ReadableTypeLookup =
        ReadableTypes.GetAlternateLookup<ReadOnlySpan<char>>();

    public static bool IsFloating(string type)
    {
        Span<char> buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return Normalise(type, buffer) is "float" or "double" or "long double";
    }

    public static bool IsPointer(string type) => type.TrimEnd() is { Length: > 0 } trimmed && trimmed[^1] is '*' or '&';

    public static bool IsReadable(ReadOnlySpan<char> type)
    {
        Span<char> buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        var normalised = Normalise(type, buffer);

        return normalised.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal)
            ? KnownEnumLookup.ContainsKey(normalised[FunctionSignature.EnumPrefix.Length..])
              || KnownFlagLookup.ContainsKey(normalised[FunctionSignature.EnumPrefix.Length..])
            : ReadableTypeLookup.Contains(normalised);
    }

    public static string FormatReferenced(string type, ulong raw)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        return Format(indirection < 0 ? type : type[..indirection], raw);
    }

    public static ulong? HoBtIdOf(string type, ulong raw)
    {
        Span<char> buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return Normalise(type, buffer) is HoBtIdType ? raw << HoBtIdShift : null;
    }

    public static string Format(string type, ulong raw)
    {
        if (type == "this" || IsPointer(type))
        {
            return raw == 0 ? "nullptr" : $"0x{raw:X}";
        }

        var normalised = Normalise(type);

        if (normalised.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal))
        {
            var enumName = normalised[FunctionSignature.EnumPrefix.Length..];

            if (KnownFlags.TryGetValue(enumName, out var flags))
            {
                return FlagsOf(flags.Value, (int)raw);
            }

            return KnownEnums.TryGetValue(enumName, out var names) && names.Value.TryGetValue((int)raw, out var name)
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
            HoBtIdType => ToNumber(raw << HoBtIdShift),
            _ => $"0x{raw:X}"
        };
    }

    private static Dictionary<int, string> GetEnumNames<T>() where T : struct, Enum
        => Enum.GetValues<T>()
               .DistinctBy(v => Convert.ToInt32(v, CultureInfo.InvariantCulture))
               .ToDictionary(v => Convert.ToInt32(v, CultureInfo.InvariantCulture), v => EventPropertyFormatter.SplitWords(v.ToString()));

    private static string FlagsOf(Dictionary<int, string> names, int value)
    {
        if (names.TryGetValue(value, out var name))
        {
            return name;
        }

        var parts = new List<string>();

        var remaining = value;

        foreach (var (flag, flagName) in names.Where(n => n.Key != 0 && (n.Key & (n.Key - 1)) == 0).OrderBy(n => n.Key))
        {
            if ((remaining & flag) == flag)
            {
                parts.Add(flagName);

                remaining &= ~flag;
            }
        }

        if (remaining != 0)
        {
            parts.Add($"0x{remaining:X}");
        }

        return string.Join(FlagSeparator, parts);
    }

    private static string ToNumber(long value)
        => value is > -(long)HexThreshold and < (long)HexThreshold
            ? value.ToString(CultureInfo.InvariantCulture)
            : $"{value.ToString(CultureInfo.InvariantCulture)} (0x{value:X})";

    private static string ToNumber(ulong value)
        => value < HexThreshold
            ? value.ToString(CultureInfo.InvariantCulture)
            : $"{value.ToString(CultureInfo.InvariantCulture)} (0x{value:X})";

    private static string Normalise(string type)
    {
        Span<char> buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return Normalise(type, buffer).ToString();
    }

    private static ReadOnlySpan<char> Normalise(ReadOnlySpan<char> type, Span<char> destination)
    {
        var length = 0;

        foreach (var range in type.Split(' '))
        {
            var part = type[range];

            if (part.IsEmpty || part is "const" or "volatile" or "__ptr64")
            {
                continue;
            }

            if (length > 0)
            {
                destination[length++] = ' ';
            }

            part.CopyTo(destination[length..]);

            length += part.Length;
        }

        return destination[..length];
    }
}
