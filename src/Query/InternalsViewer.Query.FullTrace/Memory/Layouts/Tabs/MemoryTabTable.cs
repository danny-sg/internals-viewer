using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;

public sealed class MemoryTabTable(List<MemoryTab> tabs)
{
    private const string ResourceName = "Tabs.psv";

    private const string HeaderKey = "Header";

    private const char Separator = '|';

    private const int ColumnCount = 4;

    public static MemoryTabTable Default { get; } = ReadResource();

    public List<MemoryTab> Tabs { get; } = tabs;

    public MemoryTab? Find(EngineLayout layout, string? type)
        => type is null ? null : Tabs.Find(t => layout.IsA(type, t.Type));

    internal static MemoryTabTable Read(TextReader reader)
    {
        var tabs = new List<MemoryTab>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var columns = line.Split(Separator);

            if (columns[0] == HeaderKey)
            {
                continue;
            }

            if (columns.Length != ColumnCount || !Enum.TryParse<MemoryTabLayout>(columns[2], out var layout))
            {
                throw new InvalidDataException($"{ResourceName} has a line that is not Header|Type|Layout|Trailing: {line}");
            }

            tabs.Add(new MemoryTab(columns[0], columns[1], layout, columns[3].Length == 0 ? null : columns[3]));
        }

        return new MemoryTabTable(tabs);
    }

    private static MemoryTabTable ReadResource()
    {
        var type = typeof(MemoryTabTable);

        using var stream = type.Assembly.GetManifestResourceStream($"{type.Namespace}.{ResourceName}")!;

        using var reader = new StreamReader(stream);

        return Read(reader);
    }
}
