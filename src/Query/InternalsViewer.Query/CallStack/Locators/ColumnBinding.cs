using System.Globalization;
using System.Text;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct ColumnBinding(ulong Ordinal,
                                            ushort Type,
                                            uint Parts,
                                            ulong ValueOffset,
                                            ulong LengthOffset,
                                            ulong StatusOffset)
{
    private const uint ValuePart = 0x1;

    private const uint LengthPart = 0x2;

    private const uint StatusPart = 0x4;

    private const int BaseTypeMask = 0x0FFF;

    private static readonly (int Flag, string Name)[] TypeModifiers = [(0x4000, "BYREF"), (0x2000, "ARRAY"), (0x1000, "VECTOR")];

    private static readonly Dictionary<int, string> TypeNames = new()
    {
        [0] = "EMPTY",
        [1] = "NULL",
        [2] = "I2",
        [3] = "I4",
        [4] = "R4",
        [5] = "R8",
        [6] = "CY",
        [7] = "DATE",
        [8] = "BSTR",
        [9] = "IDISPATCH",
        [10] = "ERROR",
        [11] = "BOOL",
        [12] = "VARIANT",
        [13] = "IUNKNOWN",
        [14] = "DECIMAL",
        [16] = "I1",
        [17] = "UI1",
        [18] = "UI2",
        [19] = "UI4",
        [20] = "I8",
        [21] = "UI8",
        [64] = "FILETIME",
        [72] = "GUID",
        [128] = "BYTES",
        [129] = "STR",
        [130] = "WSTR",
        [131] = "NUMERIC",
        [132] = "UDT",
        [133] = "DBDATE",
        [134] = "DBTIME",
        [135] = "DBTIMESTAMP",
        [136] = "HCHAPTER",
        [138] = "PROPVARIANT",
        [139] = "VARNUMERIC"
    };

    public string TypeName
    {
        get
        {
            var type = Type;

            var baseType = type & BaseTypeMask;

            var name = TypeNames.TryGetValue(baseType, out var known) ? known : $"Type {baseType}";

            return string.Join(' ', TypeModifiers.Where(m => (type & m.Flag) != 0).Select(m => m.Name).Append(name));
        }
    }

    public override string ToString()
    {
        var text = new StringBuilder();

        text.Append(CultureInfo.InvariantCulture, $"{Ordinal}: {TypeName}");

        if ((Parts & ValuePart) != 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" Value +0x{ValueOffset:X}");
        }

        if ((Parts & LengthPart) != 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" Length +0x{LengthOffset:X}");
        }

        if ((Parts & StatusPart) != 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" Status +0x{StatusOffset:X}");
        }

        return text.ToString();
    }
}
