using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Export.Converters;

internal sealed class PageAddressConverter : JsonConverter<PageAddress>
{
    public override PageAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = AddressParts.Split(reader.GetString(), 2);

        return new PageAddress(short.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    public override void Write(Utf8JsonWriter writer, PageAddress value, JsonSerializerOptions options)
        => writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture, $"{value.FileId}:{value.PageId}"));
}
