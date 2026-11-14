using System.Globalization;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

internal static class EngineLayoutReader
{
    private const string VersionsFolder = "Versions";

    private const string ResourceExtension = ".psv";

    private const string VersionKey = "Version";

    private const string BaseKey = "Base";

    private const string SizeKey = "Size";

    private const string HeaderKey = "Type";

    private const string PointerKind = "Pointer";

    private const string NumberKind = "Number";

    private const string HexKind = "Hex";

    private const string HexPrefix = "0x";

    private const string Wildcard = "*";

    private const char Separator = '|';

    private const char WriterSeparator = ',';

    private const char RangeSeparator = '-';

    private const char VersionSeparator = '.';

    private const int FieldColumnCount = 7;

    private const int KeyColumnCount = 3;

    private const int VersionPartCount = 4;

    public static IReadOnlyList<EngineLayout> ReadAll()
    {
        var assembly = typeof(EngineLayoutReader).Assembly;

        var prefix = $"{typeof(EngineLayoutReader).Namespace}.{VersionsFolder}.";

        var layouts = new List<EngineLayout>();

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(prefix, StringComparison.Ordinal)
                || !resource.EndsWith(ResourceExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);

            layouts.Add(Read(resource[prefix.Length..^ResourceExtension.Length], reader));
        }

        return [.. layouts.OrderBy(l => l.MinimumVersion)];
    }

    public static EngineLayout Read(string name, TextReader reader)
    {
        (Version Minimum, Version Maximum)? range = null;

        var structures = new Dictionary<string, MemoryStructure>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var columns = line.Split(Separator);

            switch (columns[0])
            {
                case VersionKey:
                    range = ReadRange(name, columns[1]);
                    break;

                case HeaderKey:
                    break;

                case BaseKey:
                    GetStructure(structures, ReadKey(name, columns)).Base = columns[2];
                    break;

                case SizeKey:
                    GetStructure(structures, ReadKey(name, columns)).Size = ReadNumber(columns[2]);
                    break;

                default:
                    var (field, count) = ReadField(name, columns);

                    GetStructure(structures, columns[0]).Add(field, count);

                    break;
            }
        }

        return range is { } versions
            ? new EngineLayout(name, versions.Minimum, versions.Maximum, structures)
            : throw new InvalidDataException($"{name} has no {VersionKey} line");
    }

    private static MemoryStructure GetStructure(Dictionary<string, MemoryStructure> structures, string type)
    {
        if (!structures.TryGetValue(type, out var structure))
        {
            structure = new MemoryStructure(type);

            structures[type] = structure;
        }

        return structure;
    }

    private static string ReadKey(string name, string[] columns)
        => columns.Length == KeyColumnCount
            ? columns[1]
            : throw new InvalidDataException($"{name} has a {columns[0]} line with {columns.Length} columns");

    private static (MemoryField Field, int Count) ReadField(string name, string[] columns)
    {
        if (columns.Length != FieldColumnCount)
        {
            throw new InvalidDataException($"{name} has a line with {columns.Length} columns: {string.Join(Separator, columns)}");
        }

        var kind = columns[4];

        if (kind is not (PointerKind or NumberKind or HexKind))
        {
            throw new InvalidDataException($"{name} has an unknown kind {kind} on {columns[0]} {columns[1]}");
        }

        var field = new MemoryField(columns[1], ReadNumber(columns[2]), ReadNumber(columns[3]), kind == HexKind)
        {
            Writers = columns[6].Length == 0 ? [] : columns[6].Split(WriterSeparator),
            IsNumber = kind != PointerKind
        };

        return (field, columns[5].Length == 0 ? 1 : ReadNumber(columns[5]));
    }

    private static int ReadNumber(string text)
        => text.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase)
            ? int.Parse(text[HexPrefix.Length..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : int.Parse(text, CultureInfo.InvariantCulture);

    private static (Version Minimum, Version Maximum) ReadRange(string name, string text)
    {
        var bounds = text.Split(RangeSeparator);

        return bounds.Length == 2
            ? (ReadVersion(bounds[0], 0), ReadVersion(bounds[1], int.MaxValue))
            : throw new InvalidDataException($"{name} has a version range that is not From-To: {text}");
    }

    private static Version ReadVersion(string text, int wildcard)
    {
        var parts = text.Split(VersionSeparator);

        var numbers = new int[VersionPartCount];

        for (var index = 0; index < VersionPartCount; index++)
        {
            numbers[index] = index < parts.Length && parts[index] != Wildcard
                ? int.Parse(parts[index], CultureInfo.InvariantCulture)
                : wildcard;
        }

        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }
}
