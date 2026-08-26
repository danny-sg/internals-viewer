namespace InternalsViewer.Internals.Columnstore.Metadata;

public sealed class DeletedRows(IReadOnlyDictionary<int, int[]> byRowGroup)
{
    public static DeletedRows None { get; } = new(new Dictionary<int, int[]>());

    public IReadOnlyDictionary<int, int[]> ByRowGroup { get; } = byRowGroup;

    public int Count => ByRowGroup.Values.Sum(r => r.Length);

    public bool IsEmpty => ByRowGroup.Count == 0;

    public int[] ForRowGroup(int rowGroupId) => ByRowGroup.TryGetValue(rowGroupId, out var rows) ? rows : [];
}
