using InternalsViewer.Query.FullTrace.Memory.History.Batches.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public sealed class MemoryHistoryBatches(MemoryHistoryStore store,
                                         Func<int, string?> getClassName,
                                         Func<int, string?> getAllocatingFunction)
{
    private static readonly string[] PartFunctionPrefixes = [BatchReader.ClassName + "::", "CBpDeepDataStore<", "RbpNormalize::"];

    private const int SearchedPositions = 8;

    private int[] Headers => field ??= [.. Enumerable.Range(0, store.Blocks.Count).Where(IsHeader)];

    private Dictionary<int, (int Header, BatchPart Part)?> Parts { get; } = [];

    private Lock PartsLock { get; } = new();

    public BatchPart? GetPart(int block) => GetOwner(block)?.Part;

    public MemoryHistoryBatch? Read(int block, TracePosition position)
    {
        var header = GetOwner(block) is { Header: var owner } && store.IsLive(owner, position)
            ? owner
            : FindOwner(block, position)?.Header ?? -1;

        if (header < 0)
        {
            return null;
        }

        var reader = new MemoryHistoryReader(store, position);

        if (BatchReader.Read(reader, store.Blocks[header].Address) is not { } state)
        {
            return null;
        }

        var vector = state.Vectors.FirstOrDefault(v => v.Buffer != 0 && store.GetBlock(v.Buffer, position) == block)
                     ?? state.Vectors.FirstOrDefault(v => GetDeepPages(reader, v).Any(p => store.GetBlock(p, position) == block));

        return new MemoryHistoryBatch(header, state, vector?.Id ?? 0, reader);
    }

    private static IEnumerable<ulong> GetDeepPages(MemoryHistoryReader reader, BatchVector vector)
    {
        if (!vector.HasDeepData)
        {
            return [];
        }

        return DeepDataReader.GetPages(reader, vector.FirstDeepPage).Append(vector.CurrentDeepPage).Where(p => p != 0);
    }

    private (int Header, BatchPart Part)? GetOwner(int block)
    {
        lock (PartsLock)
        {
            if (Parts.TryGetValue(block, out var cached))
            {
                return cached;
            }
        }

        var owner = FindPart(block);

        lock (PartsLock)
        {
            Parts[block] = owner;
        }

        return owner;
    }

    private (int Header, BatchPart Part)? FindPart(int block)
    {
        if (IsHeader(block))
        {
            return (block, new BatchPart(BatchPartType.Header));
        }

        if (getAllocatingFunction(block) is not { } function
            || !PartFunctionPrefixes.Any(p => function.StartsWith(p, StringComparison.Ordinal)))
        {
            return null;
        }

        foreach (var position in GetSearchPositions(block))
        {
            if (FindOwner(block, position) is { } owner)
            {
                return owner;
            }
        }

        return null;
    }

    private IEnumerable<TracePosition> GetSearchPositions(int block)
    {
        foreach (var position in GetSampledPositions([.. store.GetWrites(block).Select(w => w.Position)]))
        {
            yield return position;
        }

        var lifetime = store.Blocks[block];

        foreach (var header in Headers)
        {
            List<TracePosition> positions = [.. store.GetWrites(header)
                                                     .Select(w => w.Position)
                                                     .Where(p => p >= lifetime.FromPosition
                                                                 && (!lifetime.ToPosition.IsKnown || p < lifetime.ToPosition))];

            foreach (var position in GetSampledPositions(positions))
            {
                yield return position;
            }
        }
    }

    private static IEnumerable<TracePosition> GetSampledPositions(List<TracePosition> positions)
    {
        var step = Math.Max(1, positions.Count / SearchedPositions);

        for (var index = positions.Count - 1; index >= 0; index -= step)
        {
            yield return positions[index];
        }
    }

    private (int Header, BatchPart Part)? FindOwner(int block, TracePosition position)
    {
        var reader = new MemoryHistoryReader(store, position);

        foreach (var header in Headers)
        {
            if (!store.IsLive(header, position))
            {
                continue;
            }

            foreach (var (address, part) in BatchReader.GetParts(reader, store.Blocks[header].Address))
            {
                if (address != 0 && store.GetBlock(address, position) == block)
                {
                    return (header, part);
                }
            }
        }

        return null;
    }

    private bool IsHeader(int block) => getClassName(block) == BatchReader.ClassName;
}
