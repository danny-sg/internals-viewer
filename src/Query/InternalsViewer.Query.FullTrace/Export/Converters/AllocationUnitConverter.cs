using System.Text.Json;
using System.Text.Json.Serialization;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.FullTrace.Export.Converters;

internal sealed class AllocationUnitConverter(Func<long, AllocationUnit?> allocationUnitOf) : JsonConverter<AllocationUnit>
{
    private const string IdProperty = "Id";

    private const string NameProperty = "Name";

    public override AllocationUnit? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        long? id = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();

            reader.Read();

            if (name == IdProperty)
            {
                id = reader.GetInt64();
            }
            else
            {
                reader.Skip();
            }
        }

        if (id is not { } allocationUnitId)
        {
            return null;
        }

        return allocationUnitOf(allocationUnitId)
               ?? (allocationUnitId == AllocationUnit.Unknown.AllocationUnitId ? AllocationUnit.Unknown : null);
    }

    public override void Write(Utf8JsonWriter writer, AllocationUnit value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber(IdProperty, value.AllocationUnitId);

        writer.WriteString(NameProperty, value.DisplayName);

        writer.WriteEndObject();
    }
}
