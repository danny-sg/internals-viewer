namespace InternalsViewer.Query.Export;

internal sealed class ReferenceTable<T> where T : class
{
    public int Count => Items.Count;

    private Dictionary<T, int> Ids { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<int, T> Items { get; } = [];

    public T this[int id] => Items.TryGetValue(id, out var item)
        ? item
        : throw new InvalidDataException($"{typeof(T).Name} {id} is not in the export");

    public bool Contains(T item) => Ids.ContainsKey(item);

    public int Add(T item)
    {
        if (Ids.TryGetValue(item, out var id))
        {
            return id;
        }

        id = Ids.Count;

        Register(id, item);

        return id;
    }

    public int IdOf(T item) => Ids.TryGetValue(item, out var id)
        ? id
        : throw new InvalidOperationException($"{typeof(T).Name} was not added to the export");

    public void Register(int id, T item)
    {
        Ids[item] = id;

        Items[id] = item;
    }

    public IEnumerable<T> InOrder() => Items.OrderBy(i => i.Key).Select(i => i.Value);
}
