using System.Globalization;
using System.Text;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Properties;

public static class EventPropertyFormatter
{
    public static string Text(string? value) => value ?? string.Empty;

    public static string Text<T>(T value) => value?.ToString() ?? string.Empty;

    public static string Bool(bool value) => value ? "Yes" : "No";

    public static string Bool(bool? value) => value is { } flag ? Bool(flag) : string.Empty;

    public static string Enum(System.Enum? value) => value is null ? string.Empty : SplitWords(EventItemName.Get(value));

    public static string Time(DateTime value) => value.ToString("HH:mm:ss.ffffff", CultureInfo.CurrentCulture);

    public static string Number(long value, EventPropertyType type, string? format) => type switch
    {
        EventPropertyType.Microseconds => $"{value / 1000d:N3} ms",
        EventPropertyType.Bytes => $"{value:N0} bytes",
        EventPropertyType.Kilobytes => $"{value:N0} KB",
        EventPropertyType.Address => $"0x{value:X16}",
        EventPropertyType.Number => value.ToString(format ?? "N0", CultureInfo.CurrentCulture),
        _ => value.ToString(format ?? "D", CultureInfo.CurrentCulture)
    };

    public static string Number(long? value, EventPropertyType type, string? format)
        => value is { } number ? Number(number, type, format) : string.Empty;

    public static string Number(ulong value, EventPropertyType type, string? format) => type switch
    {
        EventPropertyType.Address => $"0x{value:X16}",
        _ => Number((long)value, type, format)
    };

    public static string Number(ulong? value, EventPropertyType type, string? format)
        => value is { } number ? Number(number, type, format) : string.Empty;

    public static string Number(double value, EventPropertyType type, string? format)
        => value.ToString(format ?? "G", CultureInfo.CurrentCulture);

    public static string Number(double? value, EventPropertyType type, string? format)
        => value is { } number ? Number(number, type, format) : string.Empty;

    private static string SplitWords(string name)
    {
        if (name.Length < 2 || name.Contains(' ') || name.Contains('_') || name.All(c => !char.IsLower(c)))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (i > 0 && char.IsUpper(current))
            {
                var previous = name[i - 1];

                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                {
                    builder.Append(' ');
                }
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
