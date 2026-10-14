using System.Globalization;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Indexes.Enums;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Scheduling;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.QueryStore;

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
        ["RbpFilterType"] = new(GetEnumNames<ColumnStoreFilterType>),
        ["SOS_Scheduler::Transition"] = new(GetEnumNames<TaskTransition>),
        ["Worker::CallbackType"] = new(GetEnumNames<WorkerCallbackType>),
        ["PHYOP"] = new(GetEnumNames<PhysicalOperatorType>),
        ["LOGOP"] = new(GetEnumNames<LogicalOperatorType>),
        ["QTETYPE"] = new(GetEnumNames<ExecutionElementType>),
        ["EPlanNotCachedReason"] = new(GetEnumNames<PlanNotCachedReason>),
        ["PageReadError"] = new(GetEnumNames<PageReadError>),
        ["EMDCompressLevel"] = new(GetEnumNames<CompressionType>),
        ["HoBtFactoryRingBufferRecord::HOBT_ALLOCATION_PAGE"] = new(GetEnumNames<HoBtAllocationPage>),
        ["EQDSResourceType"] = new(GetEnumNames<QueryStoreResourceType>),
        ["EQDSOperationalState"] = new(GetEnumNames<QueryStoreState>),
        ["EQDSExecType"] = new(GetEnumNames<QueryStoreExecutionType>)
    };

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>> KnownFlags = new(StringComparer.Ordinal)
    {
        ["BPool::StealOption"] = new(GetEnumNames<StealOptions>)
    };

    private static readonly HashSet<string> UnnamedEnums = new(StringComparer.Ordinal)
    {
        "ACCESSTYPE",
        "AccessMode",
        "BPool::ShortageOption",
        "BTreeMgr::BTREE_SEEK_OPTION",
        "BTreeMgr::BTREE_TRAVERSAL_MODE",
        "LatchBase::AcquireResult",
        "std::align_val_t"
    };

    private static readonly HashSet<string> ReadableTypes = new(StringComparer.Ordinal)
    {
        "bool", "short", "__int16", "unsigned short", "unsigned __int16", "int", "long", "__int32", "unsigned int", "unsigned long",
        "unsigned __int32", "__int64", "long long", "unsigned __int64", "unsigned long long", "float", "double", HoBtIdType
    };

    private static readonly HashSet<string> BufferTypes = new(StringComparer.Ordinal)
    {
        "void", "char", "signed char", "unsigned char", "wchar_t", "char8_t", "char16_t", "char32_t", "__int8", "unsigned __int8"
    };

    private static readonly string[] ClassKeywords = ["class ", "struct "];

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

    public static string? GetPointeeClass(string type)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        if (indirection <= 0 || type.AsSpan(indirection + 1).IndexOfAny('*', '&') >= 0 || type.Contains('('))
        {
            return null;
        }

        var target = Normalise(type[..indirection]);

        foreach (var keyword in ClassKeywords)
        {
            if (target.StartsWith(keyword, StringComparison.Ordinal))
            {
                target = target[keyword.Length..];
            }
        }

        return target.Length == 0
               || target.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal)
               || ReadableTypes.Contains(target)
               || BufferTypes.Contains(target)
            ? null
            : target;
    }

    public static bool IsReadable(ReadOnlySpan<char> type)
    {
        Span<char> buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        var normalised = Normalise(type, buffer);

        return normalised.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal)
            ? KnownEnumLookup.ContainsKey(normalised[FunctionSignature.EnumPrefix.Length..])
              || KnownFlagLookup.ContainsKey(normalised[FunctionSignature.EnumPrefix.Length..])
            : ReadableTypeLookup.Contains(normalised);
    }

    public static string? GetUnknownEnum(string type)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        var target = Normalise(indirection < 0 ? type : type[..indirection]);

        if (!target.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = target[FunctionSignature.EnumPrefix.Length..];

        return KnownEnums.ContainsKey(name) || KnownFlags.ContainsKey(name) || UnnamedEnums.Contains(name) ? null : name;
    }

    public static string FormatReferenced(string type, ulong raw)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        return Format(indirection < 0 ? type : type[..indirection], raw);
    }

    public static ulong? GetHoBtId(string type, ulong raw)
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

        return Read(type, raw, withHex: true) ?? $"0x{raw:X}";
    }

    public static string? Decode(string type, ulong raw)
    {
        if (type == "this" || IsPointer(type))
        {
            return raw == 0 ? "nullptr" : null;
        }

        return Read(type, raw, withHex: false);
    }

    private static string? Read(string type, ulong raw, bool withHex)
    {
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
                : ToNumber((int)raw, withHex);
        }

        return normalised switch
        {
            "bool" => (raw & 0xFF) != 0 ? "true" : "false",
            "char" or "signed char" or "__int8" => ToNumber((sbyte)raw, withHex),
            "unsigned char" or "unsigned __int8" => ToNumber((byte)raw, withHex),
            "short" or "__int16" => ToNumber((short)raw, withHex),
            "unsigned short" or "unsigned __int16" or "wchar_t" => ToNumber((ushort)raw, withHex),
            "int" or "long" or "__int32" or "enum" => ToNumber((int)raw, withHex),
            "unsigned int" or "unsigned long" or "unsigned __int32" => ToNumber((uint)raw, withHex),
            "__int64" or "long long" => ToNumber((long)raw, withHex),
            "unsigned __int64" or "unsigned long long" => ToNumber(raw, withHex),
            "float" => BitConverter.Int32BitsToSingle((int)raw).ToString(CultureInfo.InvariantCulture),
            "double" or "long double" => BitConverter.Int64BitsToDouble((long)raw).ToString(CultureInfo.InvariantCulture),
            HoBtIdType => ToNumber(raw << HoBtIdShift, withHex),
            _ => null
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

    private static string ToNumber(long value, bool withHex)
        => !withHex || value is > -(long)HexThreshold and < (long)HexThreshold
            ? value.ToString(CultureInfo.InvariantCulture)
            : $"{value.ToString(CultureInfo.InvariantCulture)} (0x{value:X})";

    private static string ToNumber(ulong value, bool withHex)
        => !withHex || value < HexThreshold
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
