using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Export.Converters;

internal sealed class RowIdentifierConverter : JsonConverter<RowIdentifier>
{
    public override RowIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = AddressParts.Split(reader.GetString(), 3);

        return new RowIdentifier(short.Parse(parts[0], CultureInfo.InvariantCulture),
                                 int.Parse(parts[1], CultureInfo.InvariantCulture),
                                 ushort.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    public override void Write(Utf8JsonWriter writer, RowIdentifier value, JsonSerializerOptions options)
        => writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture,
                                                 $"{value.PageAddress.FileId}:{value.PageAddress.PageId}:{value.SlotId}"));
}
