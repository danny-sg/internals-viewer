using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

internal sealed class DeclaredTypeMap
{
    private DeclaredTypeMap(Dictionary<int, (string Type, int Source)> types)
    {
        Types = types;
    }

    public static DeclaredTypeMap Empty { get; } = new([]);

    private Dictionary<int, (string Type, int Source)> Types { get; }

    public string? Get(int block) => Types.TryGetValue(block, out var found) ? found.Type : null;

    public int? GetSource(int block) => Types.TryGetValue(block, out var found) ? found.Source : null;

    public static DeclaredTypeMap Build(MemoryHistoryStore store, Func<int, string?> getClass, Func<int, string?> getContentClass)
    {
        var types = new Dictionary<int, (string Type, int Source)>();

        var pending = new Queue<(int Block, string Type)>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            if (getClass(block) is { } type && HasTargets(store.Layout, type))
            {
                pending.Enqueue((block, type));
            }
        }

        while (pending.TryDequeue(out var source))
        {
            foreach (var target in GetTargets(store, source.Block, source.Type))
            {
                if (!types.ContainsKey(target.Block) && getClass(target.Block) is null && getContentClass(target.Block) is null)
                {
                    types[target.Block] = (target.Type, source.Block);

                    pending.Enqueue(target);
                }
            }
        }

        return types.Count == 0 ? Empty : new DeclaredTypeMap(types);
    }

    private static bool HasTargets(EngineLayout layout, string type) => layout.GetFields(type).Exists(f => f.Target is not null);

    private static IEnumerable<(int Block, string Type)> GetTargets(MemoryHistoryStore store, int block, string type)
    {
        var fields = store.Layout.GetFields(type).FindAll(f => f.Target is not null);

        var address = store.Blocks[block].Address;

        foreach (var write in store.GetWrites(block))
        {
            foreach (var field in fields)
            {
                if (write.Offset >= field.Offset + field.Length || write.Offset + write.Length <= field.Offset)
                {
                    continue;
                }

                if (new MemoryHistoryReader(store, write.Position).TryRead(address + (ulong)field.Offset, out ulong value)
                    && store.GetBlock(value, write.Position) is >= 0 and var target
                    && store.Blocks[target] is { IsStack: false } found
                    && found.Address == value)
                {
                    yield return (target, field.Target!);
                }
            }
        }
    }
}
