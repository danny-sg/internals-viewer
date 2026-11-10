using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public sealed partial record TraceTrackRule
{
    private const string PredefinedFile = ".PredefinedTracks.json";

    private static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task WriteAsync(Stream stream, IReadOnlyList<TraceTrackRule> definitions, CancellationToken cancellationToken)
        => JsonSerializer.SerializeAsync(stream, definitions, Options, cancellationToken);

    public static IReadOnlyList<TraceTrackRule> GetPredefined()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(PredefinedFile, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(name)!;

        return Read(stream);
    }

    public static void Write(Stream stream, IReadOnlyList<TraceTrackRule> definitions)
        => JsonSerializer.Serialize(stream, definitions, Options);

    public static IReadOnlyList<TraceTrackRule> Read(Stream stream)
        => JsonSerializer.Deserialize<List<TraceTrackRule>>(stream, Options) ?? [];

    public static async Task<IReadOnlyList<TraceTrackRule>> ReadAsync(Stream stream, CancellationToken cancellationToken)
        => await JsonSerializer.DeserializeAsync<List<TraceTrackRule>>(stream, Options, cancellationToken) ?? [];
}
