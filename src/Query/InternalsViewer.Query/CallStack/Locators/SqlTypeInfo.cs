namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct SqlTypeInfo(byte TypeId, byte Flags, ushort MaxLength, byte Precision, byte Scale, uint Collation)
{
    private const byte NotNullFlag = 1;

    private const ushort MaxLengthUnlimited = 0xFFFF;

    private static readonly Dictionary<byte, string> Names = new()
    {
        [34] = "image",
        [35] = "text",
        [36] = "uniqueidentifier",
        [40] = "date",
        [41] = "time",
        [42] = "datetime2",
        [43] = "datetimeoffset",
        [48] = "tinyint",
        [52] = "smallint",
        [56] = "int",
        [58] = "smalldatetime",
        [59] = "real",
        [60] = "money",
        [61] = "datetime",
        [62] = "float",
        [98] = "sql_variant",
        [99] = "ntext",
        [104] = "bit",
        [106] = "decimal",
        [108] = "numeric",
        [122] = "smallmoney",
        [127] = "bigint",
        [165] = "varbinary",
        [167] = "varchar",
        [173] = "binary",
        [175] = "char",
        [189] = "timestamp",
        [231] = "nvarchar",
        [239] = "nchar",
        [240] = "clr",
        [241] = "xml",
        [244] = "json",
        [250] = "cursor"
    };

    public bool IsNotNull => (Flags & NotNullFlag) != 0;

    public static SqlTypeInfo From(ulong header, ulong details)
        => new((byte)header, (byte)(header >> 8), (ushort)details, (byte)(details >> 16), (byte)(details >> 24), (uint)(details >> 32));

    public override string ToString()
    {
        var name = Names.TryGetValue(TypeId, out var known) ? $"{known}{GetSize()}" : $"type 0x{TypeId:X2}";

        return IsNotNull ? $"{name} NOT NULL" : name;
    }

    private string GetSize() => TypeId switch
    {
        165 or 167 or 173 or 175 when MaxLength > 0 => MaxLength == MaxLengthUnlimited ? "(max)" : $"({MaxLength})",
        231 or 239 when MaxLength > 0 => MaxLength == MaxLengthUnlimited ? "(max)" : $"({MaxLength / 2})",
        106 or 108 when Precision > 0 => $"({Precision},{Scale})",
        41 or 42 or 43 when Precision > 0 => $"({Scale})",
        _ => string.Empty
    };
}
