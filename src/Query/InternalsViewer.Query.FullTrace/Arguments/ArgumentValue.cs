using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using System.Globalization;
using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Indexes.Enums;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Symbols.Enums;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Parsing;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Scheduling;
using InternalsViewer.Query.Memory.Enums;
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

    private const int MaximumStackType = TypeNameText.MaximumStackType;

    private const int AddressSize = sizeof(ulong);

    private const string FlagSeparator = " | ";

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>> KnownEnums = new(StringComparer.Ordinal)
    {
        ["LatchBase::LATCH_TYPE"] = new(GetEnumNames<LatchMode>),
        ["LatchBase::LatchClass"] = new(GetEnumNames<LatchClass>),
        ["BTreeMgr::VR_STATUS"] = new(GetEnumNames<BTreeRootStatus>),
        ["BTreeMgr::BTREE_SEEK_OPTION"] = new(GetEnumNames<BTreeSeekOption>),
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
        ["EQDSExecType"] = new(GetEnumNames<QueryStoreExecutionType>),
        ["SOSHOST_MEMOBJ_ID"] = new(GetMemoryObjectTypeNames)
    };

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>> KnownFlags = new(StringComparer.Ordinal)
    {
        ["BPool::StealOption"] = new(GetEnumNames<StealOptions>),
        ["DBACCESSORFLAGS"] = new(() => new Dictionary<int, string>
        {
            [0x0] = "INVALID",
            [0x1] = "PASSBYREF",
            [0x2] = "ROWDATA",
            [0x4] = "PARAMETERDATA",
            [0x8] = "OPTIMIZED",
            [0x10] = "INHERITED"
        })
    };

    private static readonly HashSet<string> UnnamedFlags = new(StringComparer.Ordinal)
    {
        "AccessorFlagEx"
    };

    private static readonly HashSet<string> UnnamedEnums = new(StringComparer.Ordinal)
    {
        "ACCESSTYPE",
        "AccessMode",
        "BPool::ShortageOption",
        "BTreeMgr::BTREE_TRAVERSAL_MODE",
        "LatchBase::AcquireResult",
        "std::align_val_t"
    };

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>>.AlternateLookup<ReadOnlySpan<char>> KnownEnumLookup =
        KnownEnums.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly Dictionary<string, Lazy<Dictionary<int, string>>>.AlternateLookup<ReadOnlySpan<char>> KnownFlagLookup =
        KnownFlags.GetAlternateLookup<ReadOnlySpan<char>>();

    public static bool IsFloating(string type)
    {
        var buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return TypeNameText.GetPrimitive(TypeNameText.Normalise(type, buffer)) is { Kind: PrimitiveTypeKind.Float };
    }

    public static bool IsAddressSized(string type)
    {
        var buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return TypeNameText.GetPrimitive(TypeNameText.Normalise(type, buffer)) is { IsInteger: true, Size: AddressSize };
    }

    public static bool IsPointer(string type) => type.TrimEnd() is { Length: > 0 } trimmed && trimmed[^1] is '*' or '&';

    public static string? GetPointeeClass(ReadOnlySpan<char> type) => GetPointeeClass(type, null);

    public static bool IsClassPointer(ReadOnlySpan<char> type)
    {
        var buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return TryGetPointeeClass(type, buffer, out _);
    }

    internal static string? GetPointeeClass(ReadOnlySpan<char> type, StringInternPool? strings)
    {
        var buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        if (!TryGetPointeeClass(type, buffer, out var className))
        {
            return null;
        }

        return strings is null ? className.ToString() : strings.Intern(className);
    }

    public static bool IsReadable(ReadOnlySpan<char> type)
    {
        var buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        var normalised = TypeNameText.Normalise(type, buffer);

        return normalised.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal)
            ? KnownEnumLookup.ContainsKey(normalised[FunctionSignature.EnumPrefix.Length..])
              || KnownFlagLookup.ContainsKey(normalised[FunctionSignature.EnumPrefix.Length..])
            : normalised is HoBtIdType
              || TypeNameText.GetPrimitive(normalised) is { IsCharacter: false, Kind: not PrimitiveTypeKind.Void };
    }

    public static string? GetUnknownEnum(string type)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        var target = TypeNameText.Normalise(indirection < 0 ? type : type[..indirection]);

        if (!target.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = target[FunctionSignature.EnumPrefix.Length..];

        return KnownEnums.ContainsKey(name) || KnownFlags.ContainsKey(name) || UnnamedEnums.Contains(name) || UnnamedFlags.Contains(name)
            ? null
            : name;
    }

    public static string FormatReferenced(string type, ulong raw)
    {
        var indirection = type.IndexOfAny(['*', '&']);

        return Format(indirection < 0 ? type : type[..indirection], raw);
    }

    public static ulong? GetHoBtId(string type, ulong raw)
    {
        var buffer = type.Length <= MaximumStackType ? stackalloc char[type.Length] : new char[type.Length];

        return TypeNameText.Normalise(type, buffer) is HoBtIdType ? raw << HoBtIdShift : null;
    }

    public static string Format(string type, ulong raw)
    {
        if (type == "this" || IsPointer(type))
        {
            return raw == 0 ? "nullptr" : $"0x{raw:X}";
        }

        return Read(type, raw, withHex: true) ?? $"0x{raw:X}";
    }

    public static Int128 ToInteger(string type, ulong raw)
    {
        if (type == "this" || IsPointer(type))
        {
            return raw;
        }

        var normalised = TypeNameText.Normalise(type);

        if (normalised.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal) || normalised is FunctionSignature.EnumKeyword)
        {
            return (int)raw;
        }

        if (normalised is HoBtIdType)
        {
            return (Int128)raw << HoBtIdShift;
        }

        return TypeNameText.GetPrimitive(normalised) switch
        {
            { Kind: PrimitiveTypeKind.Bool } => (byte)raw,
            { Kind: PrimitiveTypeKind.SignedInteger } primitive => ToSigned(raw, primitive.Size),
            { Kind: PrimitiveTypeKind.UnsignedInteger } primitive => ToUnsigned(raw, primitive.Size),
            _ => raw
        };
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
        var normalised = TypeNameText.Normalise(type);

        if (normalised.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal))
        {
            var enumName = normalised[FunctionSignature.EnumPrefix.Length..];

            if (KnownFlags.TryGetValue(enumName, out var flags))
            {
                return GetFlags(flags.Value, (int)raw);
            }

            if (UnnamedFlags.Contains(enumName))
            {
                return $"0x{(uint)raw:X}";
            }

            return KnownEnums.TryGetValue(enumName, out var names) && names.Value.TryGetValue((int)raw, out var name)
                ? name
                : ToNumber((int)raw, withHex);
        }

        if (normalised is HoBtIdType)
        {
            return ToNumber(raw << HoBtIdShift, withHex);
        }

        return TypeNameText.GetPrimitive(normalised) switch
        {
            { Kind: PrimitiveTypeKind.Bool } => (raw & 0xFF) != 0 ? "true" : "false",
            { Kind: PrimitiveTypeKind.SignedInteger } primitive => ToNumber(ToSigned(raw, primitive.Size), withHex),
            { Kind: PrimitiveTypeKind.UnsignedInteger } primitive => ToNumber(ToUnsigned(raw, primitive.Size), withHex),
            { Kind: PrimitiveTypeKind.Float, Size: sizeof(float) }
                => BitConverter.Int32BitsToSingle((int)raw).ToString(CultureInfo.InvariantCulture),
            { Kind: PrimitiveTypeKind.Float } => BitConverter.Int64BitsToDouble((long)raw).ToString(CultureInfo.InvariantCulture),
            null when normalised is FunctionSignature.EnumKeyword => ToNumber((int)raw, withHex),
            _ => null
        };
    }

    private static long ToSigned(ulong raw, int size) => size switch
    {
        sizeof(sbyte) => (sbyte)raw,
        sizeof(short) => (short)raw,
        sizeof(int) => (int)raw,
        _ => (long)raw
    };

    private static ulong ToUnsigned(ulong raw, int size) => size switch
    {
        sizeof(byte) => (byte)raw,
        sizeof(ushort) => (ushort)raw,
        sizeof(uint) => (uint)raw,
        _ => raw
    };

    private static Dictionary<int, string> GetEnumNames<T>() where T : struct, Enum
        => Enum.GetValues<T>()
               .DistinctBy(v => Convert.ToInt32(v, CultureInfo.InvariantCulture))
               .ToDictionary(v => Convert.ToInt32(v, CultureInfo.InvariantCulture), v => EventPropertyFormatter.SplitWords(v.ToString()));

    private static Dictionary<int, string> GetMemoryObjectTypeNames()
        => Enum.GetValues<MemoryObjectType>().ToDictionary(v => (int)v, MemoryObjectNameLookup.GetName);

    private static string GetFlags(Dictionary<int, string> names, int value)
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

    private static bool TryGetPointeeClass(ReadOnlySpan<char> type, Span<char> buffer, out ReadOnlySpan<char> className)
    {
        className = default;

        var indirection = type.IndexOfAny('*', '&');

        if (indirection <= 0 || type[(indirection + 1)..].IndexOfAny('*', '&') >= 0 || type.Contains('('))
        {
            return false;
        }

        var target = TypeNameText.RemoveClassKeyword(TypeNameText.Normalise(type[..indirection], buffer));

        if (target.IsEmpty
            || target.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal)
            || target is HoBtIdType
            || TypeNameText.GetPrimitive(target) is not null)
        {
            return false;
        }

        className = target;

        return true;
    }
}
