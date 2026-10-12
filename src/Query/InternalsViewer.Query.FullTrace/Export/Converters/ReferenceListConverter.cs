using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalsViewer.Query.FullTrace.Export.Converters;

internal sealed class ReferenceListConverter<TCollection, T, TBase>(ReferenceTable<TBase> table) : JsonConverter<TCollection>
    where TCollection : class, IEnumerable<T>
    where TBase : class
    where T : class, TBase
{
    public override TCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected a list of {typeof(T).Name} ids");
        }

        var items = new List<T>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            items.Add((T)table[reader.GetInt32()]);
        }

        return typeToConvert.IsArray ? (TCollection)(object)items.ToArray() : (TCollection)(object)items;
    }

    public override void Write(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
        {
            writer.WriteNumberValue(table.IdOf(item));
        }

        writer.WriteEndArray();
    }
}
