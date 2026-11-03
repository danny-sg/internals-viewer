using System.Buffers.Binary;
using System.Text;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed class MagicBytesMatcher(MemoryHistoryStore store)
{
    private static Dictionary<(int Offset, uint Magic), string> ClassNames { get; } = new()
    {
        [(0x1C, GetMagic("BPSP"))] = "CBpSoftPartitionPageHeader",
        [(0x48, GetMagic("BPOP"))] = "CBpOverflowBucketPage"
    };

    private static int[] Offsets { get; } = [.. ClassNames.Keys.Select(k => k.Offset).Distinct()];

    private BlockCache<string?> Names => field ??= new(FindClassName);

    public string? GetClassName(int block) => Names.Get(block);

    private static uint GetMagic(string magic) => BinaryPrimitives.ReadUInt32LittleEndian(Encoding.ASCII.GetBytes(magic));

    private string? FindClassName(int block)
    {
        foreach (var write in store.GetWrites(block))
        {
            foreach (var offset in Offsets)
            {
                if (store.TryGetWrittenValue(write, offset, out var magic) && ClassNames.TryGetValue((offset, magic), out var className))
                {
                    return className;
                }
            }
        }

        return null;
    }
}
