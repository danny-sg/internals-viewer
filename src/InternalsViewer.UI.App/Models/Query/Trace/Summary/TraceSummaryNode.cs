using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

namespace InternalsViewer.UI.App.Models.Query.Trace.Summary;

public sealed partial class TraceSummaryNode(string name, string detail, string key) : ObservableObject
{
    [ObservableProperty]
    private string _value = string.Empty;

    public string Name { get; } = name;

    public string Detail { get; } = detail;

    public string Key { get; } = key;

    public int Entry { get; init; } = -1;

    public int Block { get; init; } = -1;

    public ulong Address { get; init; }

    public TracePosition? Position { get; init; }

    public ulong HashTableGroup { get; init; }

    public TraceHashPart? HashTablePart { get; init; }

    public bool HasChildren { get; init; }

    public List<int>? Members { get; init; }

    public List<TraceSummaryNode>? Children { get; init; }

    public bool IsObject => Block >= 0;
}
