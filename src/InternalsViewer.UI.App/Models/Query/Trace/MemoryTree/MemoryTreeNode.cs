using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

namespace InternalsViewer.UI.App.Models.Query.Trace.MemoryTree;

public sealed class MemoryTreeNode(string name, string detail, string key)
{
    public string Name { get; } = name;

    public string Detail { get; } = detail;

    public string Key { get; } = key;

    public int Entry { get; init; } = -1;

    public int Block { get; init; } = -1;

    public ulong Address { get; init; }

    public ulong HashTableGroup { get; init; }

    public TraceHashPart? HashTablePart { get; init; }

    public bool HasChildren { get; init; }

    public List<int>? Members { get; init; }

    public bool IsObject => Block >= 0;
}
