using System.Text.Json;

namespace InternalsViewer.Query.FullTrace.Export.Converters;

internal static class AddressParts
{
    public static string[] Split(string? value, int count)
    {
        var parts = (value ?? string.Empty).Split(':');

        return parts.Length == count ? parts : throw new JsonException($"'{value}' is not an address with {count} parts");
    }
}
