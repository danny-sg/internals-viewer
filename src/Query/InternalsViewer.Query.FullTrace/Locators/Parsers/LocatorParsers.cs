using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class LocatorParsers
{
    public LocatorParsers(ImageText? images = null)
    {
        LocatorParser[] parsers =
        [
            new PageLocatorParser(),
            new RowLocatorParser(),
            new ColumnStoreObjectLocatorParser(),
            new ValueLocatorParser(LocatorValueType.Referenced),
            new LogSequenceNumberLocatorParser(),
            new TextLocatorParser(isWide: false, images),
            new TextLocatorParser(isWide: true, images),
            new TypeInfoLocatorParser(),
            new VariantLocatorParser()
        ];

        Parsers = parsers.ToDictionary(p => p.ValueType);

        var names = new Dictionary<string, (LocatorParser Parser, LocatorTypeName Name)>(StringComparer.Ordinal);

        foreach (var parser in parsers)
        {
            foreach (var name in parser.TypeNames)
            {
                names.Add(name.Name, (parser, name));
            }
        }

        Names = names.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public static LocatorParsers Default { get; } = new();

    public IEnumerable<LocatorParser> All => Parsers.Values;

    private Dictionary<LocatorValueType, LocatorParser> Parsers { get; }

    private Dictionary<string, (LocatorParser Parser, LocatorTypeName Name)>.AlternateLookup<ReadOnlySpan<char>> Names { get; }

    public LocatorParser? Get(LocatorValueType valueType) => Parsers.GetValueOrDefault(valueType);

    public (LocatorParser Parser, LocatorTypeName Name)? GetKnownType(ReadOnlySpan<char> name)
        => Names.TryGetValue(name, out var found) ? found : null;
}
