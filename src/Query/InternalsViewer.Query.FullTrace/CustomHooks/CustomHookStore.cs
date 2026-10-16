namespace InternalsViewer.Query.FullTrace.CustomHooks;

public sealed class CustomHookStore(IReadOnlyList<CustomHookCapture> captures)
{
    public static CustomHookStore Empty { get; } = new([]);

    public int Count => Captures.Count;

    private IReadOnlyList<CustomHookCapture> Captures { get; } = captures;

    private Dictionary<string, CustomHookCapture[]> Names
        => field ??= Captures.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.ToArray());

    public IReadOnlyList<CustomHookCapture> GetCaptures(string name) => Names.GetValueOrDefault(name) ?? [];
}
