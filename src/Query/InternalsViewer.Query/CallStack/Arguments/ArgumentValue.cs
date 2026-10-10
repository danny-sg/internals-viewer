using System.Globalization;

namespace InternalsViewer.Query.CallStack.Arguments;

public static class ArgumentValue
{
    private const ulong HexThreshold = 0x10000;

    public static bool IsFloating(string type) => Normalise(type) is "float" or "double" or "long double";

    public static bool IsPointer(string type) => type.TrimEnd() is { Length: > 0 } trimmed && trimmed[^1] is '*' or '&';

    public static string Format(string type, ulong raw)
    {
        if (type == "this" || IsPointer(type))
        {
            return raw == 0 ? "nullptr" : $"0x{raw:X}";
        }

        return Normalise(type) switch
        {
            "bool" => (raw & 0xFF) != 0 ? "true" : "false",
            "char" or "signed char" or "__int8" => Number((sbyte)raw),
            "unsigned char" or "unsigned __int8" => Number((byte)raw),
            "short" or "__int16" => Number((short)raw),
            "unsigned short" or "unsigned __int16" or "wchar_t" => Number((ushort)raw),
            "int" or "long" or "__int32" or "enum" => Number((int)raw),
            "unsigned int" or "unsigned long" or "unsigned __int32" => Number((uint)raw),
            "__int64" or "long long" => Number((long)raw),
            "unsigned __int64" or "unsigned long long" => Number(raw),
            "float" => BitConverter.Int32BitsToSingle((int)raw).ToString(CultureInfo.InvariantCulture),
            "double" or "long double" => BitConverter.Int64BitsToDouble((long)raw).ToString(CultureInfo.InvariantCulture),
            _ => $"0x{raw:X}"
        };
    }

    private static string Number(long value)
        => value is > -(long)HexThreshold and < (long)HexThreshold
            ? value.ToString(CultureInfo.InvariantCulture)
            : $"{value.ToString(CultureInfo.InvariantCulture)} (0x{value:X})";

    private static string Number(ulong value)
        => value < HexThreshold
            ? value.ToString(CultureInfo.InvariantCulture)
            : $"{value.ToString(CultureInfo.InvariantCulture)} (0x{value:X})";

    private static string Normalise(string type)
        => string.Join(' ', type.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                .Where(part => part is not ("const" or "volatile" or "__ptr64")));
}
