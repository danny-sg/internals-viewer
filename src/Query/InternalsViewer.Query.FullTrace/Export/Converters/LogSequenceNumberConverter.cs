using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.Export.Converters;

internal sealed class LogSequenceNumberConverter : JsonConverter<LogSequenceNumber>
{
    public override LogSequenceNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = AddressParts.Split(reader.GetString(), 3);

        return new LogSequenceNumber(int.Parse(parts[0], CultureInfo.InvariantCulture),
                                     int.Parse(parts[1], CultureInfo.InvariantCulture),
                                     short.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    public override void Write(Utf8JsonWriter writer, LogSequenceNumber value, JsonSerializerOptions options)
        => writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture,
                                                 $"{value.VirtualLogFile}:{value.FileOffset}:{value.RecordSequence}"));
}
