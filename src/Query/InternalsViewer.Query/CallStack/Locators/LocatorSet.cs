namespace InternalsViewer.Query.CallStack.Locators;

public sealed class LocatorSet
{
    private const int ListedLocators = 10;

    public int PageCount => Pages.Count;

    public int RowCount => Rows.Count;

    public int ObjectCount => Objects.Count;

    public int Count => PageCount + RowCount + ObjectCount;

    public string Label => Count == 1
        ? Ordered().First().ToString()
        : string.Join(", ",
                      new[] { Counted(PageCount, "Page"), Counted(RowCount, "Row"), Counted(ObjectCount, "Columnstore Object") }
                          .Where(c => c.Length > 0));

    public string Detail
    {
        get
        {
            var listed = Ordered().Select(l => l.ToString()).ToList();

            var shown = string.Join(", ", listed.Take(ListedLocators));

            return listed.Count > ListedLocators ? $"{shown}, etc." : shown;
        }
    }

    private HashSet<ulong> Pages { get; } = [];

    private HashSet<ulong> Rows { get; } = [];

    private HashSet<ulong> RowPages { get; } = [];

    private HashSet<(ulong Value, ulong Detail)> Objects { get; } = [];

    public void Add(Locator locator)
    {
        switch (locator.ValueType)
        {
            case LocatorValueType.Row:
            {
                Rows.Add(locator.Value);

                RowPages.Add(locator.PageKey);

                break;
            }

            case LocatorValueType.ColumnStoreObject:
            {
                Objects.Add((locator.Value, locator.Detail));

                break;
            }

            default:
            {
                Pages.Add(locator.PageKey);

                break;
            }
        }
    }

    public void Add(LocatorSet other)
    {
        Pages.UnionWith(other.Pages);

        Rows.UnionWith(other.Rows);

        RowPages.UnionWith(other.RowPages);

        Objects.UnionWith(other.Objects);
    }

    public IEnumerable<Locator> Ordered()
        => Pages.Order()
                .Select(p => new Locator(0, LocatorValueType.Page, p))
                .Concat(Rows.Order().Select(r => new Locator(0, LocatorValueType.Row, r)))
                .Concat(Objects.Order().Select(o => new Locator(0, LocatorValueType.ColumnStoreObject, o.Value, o.Detail)));

    public bool Contains(LocatorSearch search) => search switch
    {
        { IsColumnStore: true }
            => Objects.Any(o => search.Matches(new Locator(0, LocatorValueType.ColumnStoreObject, o.Value, o.Detail))),
        { Slot: not null } => Rows.Contains(search.Key),
        _ => Pages.Contains(search.Key) || RowPages.Contains(search.Key)
    };

    private static string Counted(int count, string noun) => count switch
    {
        0 => string.Empty,
        1 => $"1 {noun}",
        _ => $"{count:N0} {noun}s"
    };
}
