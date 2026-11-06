using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed partial record TraceTickDefinition
{
    private const string PredefinedFile = ".PredefinedTracks.json";

    private static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task WriteAsync(Stream stream, IReadOnlyList<TraceTickDefinition> definitions, CancellationToken cancellationToken)
        => JsonSerializer.SerializeAsync(stream, definitions, Options, cancellationToken);

    public static IReadOnlyList<TraceTickDefinition> GetPredefined()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(PredefinedFile, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(name)!;

        return Read(stream);
    }

    public static void Write(Stream stream, IReadOnlyList<TraceTickDefinition> definitions)
        => JsonSerializer.Serialize(stream, definitions, Options);

    public static IReadOnlyList<TraceTickDefinition> Read(Stream stream)
        => JsonSerializer.Deserialize<List<TraceTickDefinition>>(stream, Options) ?? [];

    public static async Task<IReadOnlyList<TraceTickDefinition>> ReadAsync(Stream stream, CancellationToken cancellationToken)
        => await JsonSerializer.DeserializeAsync<List<TraceTickDefinition>>(stream, Options, cancellationToken) ?? [];
}
