using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalsViewer.Query.Export.Converters;

internal sealed class ReferenceConverter<T, TBase>(ReferenceTable<TBase> table) : JsonConverter<T>
    where TBase : class
    where T : class, TBase
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => (T)table[reader.GetInt32()];

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteNumberValue(table.IdOf(value));
}
