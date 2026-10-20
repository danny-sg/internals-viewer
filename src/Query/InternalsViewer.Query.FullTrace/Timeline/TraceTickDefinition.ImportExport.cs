using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial record TraceTickDefinition
{
    private static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task WriteAsync(Stream stream, IReadOnlyList<TraceTickDefinition> definitions, CancellationToken cancellationToken)
        => JsonSerializer.SerializeAsync(stream, definitions, Options, cancellationToken);

    public static async Task<IReadOnlyList<TraceTickDefinition>> ReadAsync(Stream stream, CancellationToken cancellationToken)
        => await JsonSerializer.DeserializeAsync<List<TraceTickDefinition>>(stream, Options, cancellationToken) ?? [];
}
