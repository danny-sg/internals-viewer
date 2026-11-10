using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Models.Query.Simulation.Hash;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryHashTableViewModel : ObservableObject, IPlayheadDocument
{
    private const string UnknownValue = "?";

    private static readonly TimeSpan LoadInterval = TimeSpan.FromMilliseconds(50);

    [ObservableProperty]
    private IReadOnlyList<string> _partitionNames = [];

    [ObservableProperty]
    private int _selectedPartition = -1;

    [ObservableProperty]
    private IReadOnlyList<HashBucketModel> _buckets = [];

    [ObservableProperty]
    private IReadOnlyList<HashColumnModel> _columns = [];

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _partitionTitle = string.Empty;

    [ObservableProperty]
    private string _partitionSummary = string.Empty;

    public MemoryHashTableViewModel(MemoryHistoryViewModel history, TraceCallStore calls, ulong group, int partition)
    {
        History = history;

        Group = group;

        Accesses = TraceHashTableReader.GetAccesses(calls, group);

        Partition = partition;

        Loader = new PlayheadLoader<LoadedTable?>(LoadInterval, Apply);
    }

    public string Title => "Hash Table";

    private MemoryHistoryViewModel History { get; }

    private ulong Group { get; }

    private List<TraceHashAccess> Accesses { get; }

    private PlayheadLoader<LoadedTable?> Loader { get; }

    private LoadedTable? Loaded { get; set; }

    private int Partition { get; set; }

    private int? AccessPartition { get; set; }

    public void SetPlayhead(TracePosition? position)
    {
        if (position is not { } value)
        {
            return;
        }

        var history = History;

        var group = Group;

        var accesses = Accesses;

        Loader.Request(() => Read(history, group, accesses, value));
    }

    public void ShowPartition(int partition)
    {
        Partition = partition;

        SelectedPartition = partition;

        ShowPartition();
    }

    public void Close() => Loader.Cancel();

    partial void OnSelectedPartitionChanged(int value)
    {
        if (value < 0 || value == Partition)
        {
            return;
        }

        Partition = value;

        ShowPartition();
    }

    private static LoadedTable? Read(MemoryHistoryViewModel history, ulong group, List<TraceHashAccess> accesses, TracePosition position)
    {
        if (TraceHashTableReader.Read(history.Store, group, position) is not { } table)
        {
            return null;
        }

        var span = CollectionsMarshal.AsSpan(accesses);

        var index = SortedSearch.FirstAfter<TraceHashAccess, TracePosition>(span, position, a => a.Position) - 1;

        TraceHashAccess? access = index >= 0 ? accesses[index] : null;

        var rows = new Dictionary<ulong, IReadOnlyList<(string Name, string Value)>>();

        var pages = new Dictionary<int, WorkfilePage?>();

        foreach (var bucket in table.Partitions.SelectMany(p => p.Buckets))
        {
            if (bucket.Row != 0 && ReadRow(history, pages, bucket.Row, position) is { } values)
            {
                rows[bucket.Row] = values;
            }
        }

        return new LoadedTable(table, access, access is { } value ? table.GetLocation(value.Hash) : null, rows);
    }

    private static IReadOnlyList<(string Name, string Value)>? ReadRow(MemoryHistoryViewModel history,
                                                                        Dictionary<int, WorkfilePage?> pages,
                                                                        ulong row,
                                                                        TracePosition position)
    {
        var store = history.Store;

        var block = store.GetBlock(row, position);

        if (block < 0)
        {
            return null;
        }

        if (!pages.TryGetValue(block, out var page))
        {
            var count = store.GetWriteCount(block, position);

            page = count > 0 ? WorkfilePageReader.Read(store.GetImage(block, count - 1).Data, history.GetWorkfileLayout(block)) : null;

            pages[block] = page;
        }

        var offset = (int)(row - store.Blocks[block].Address);

        return page?.Records.FirstOrDefault(r => r.Offset == offset) is { } record
            ? [.. record.Fields.Select(f => (f.Name, f.Value))]
            : null;
    }

    private static List<HashColumnModel> CreateColumns(IReadOnlyList<(string Name, string Value)>? fields)
        =>
        [
            new() { Header = "Bucket", IsMonospace = true, Width = 62 },
            new() { Header = "Rows", IsMonospace = true, Width = 52 },
            new() { Header = "Key", IsMonospace = true, Width = 92 },
            new() { Header = "Row", IsMonospace = true, Width = 140 },
            .. (fields ?? []).Select(f => new HashColumnModel { Header = f.Name })
        ];

    private static string GetText(long value) => value.ToString(CultureInfo.CurrentCulture);

    private void Apply(LoadedTable? loaded)
    {
        Loaded = loaded;

        if (loaded is null)
        {
            Summary = string.Empty;

            Buckets = [];

            return;
        }

        var partitions = loaded.Table.Partitions;

        if (PartitionNames.Count != partitions.Count)
        {
            PartitionNames = [.. partitions.Select(p => GetText(p.Index))];
        }

        Summary = $"{partitions.Count} Partitions, {partitions.Sum(p => p.RowCount)} Rows";

        if (loaded.Location is { Partition: var partition } && partition != AccessPartition)
        {
            AccessPartition = partition;

            Partition = partition;
        }

        SelectedPartition = Partition;

        ShowPartition();
    }

    private void ShowPartition()
    {
        if (Loaded is not { } loaded || Partition < 0 || Partition >= loaded.Table.Partitions.Count)
        {
            Buckets = [];

            return;
        }

        var partition = loaded.Table.Partitions[Partition];

        var current = loaded.Location is { } location && location.Partition == Partition ? location.Bucket : -1;

        var columns = CreateColumns(loaded.Rows.Values.FirstOrDefault());

        var buckets = new List<HashBucketModel>();

        var isCurrentShown = false;

        foreach (var bucket in partition.Buckets)
        {
            if (!isCurrentShown && current >= 0 && current < bucket.Index)
            {
                buckets.Add(CreateEmptyBucket(columns, current));

                isCurrentShown = true;
            }

            isCurrentShown |= bucket.Index == current;

            buckets.Add(CreateBucket(columns, bucket, loaded, bucket.Index == current));
        }

        if (!isCurrentShown && current >= 0)
        {
            buckets.Add(CreateEmptyBucket(columns, current));
        }

        Columns = columns;

        Buckets = buckets;

        PartitionTitle = $"Partition {partition.Index}";

        PartitionSummary = $"{partition.RowCount} Rows in {partition.BucketCount} Buckets";
    }

    private static HashBucketModel CreateBucket(List<HashColumnModel> columns, TraceHashBucket bucket, LoadedTable loaded, bool isCurrent)
    {
        var model = new HashBucketModel { Index = bucket.Index, IsCurrent = isCurrent };

        var fields = loaded.Rows.GetValueOrDefault(bucket.Row) ?? [];

        string[] values =
        [
            GetText(bucket.Index),
            GetText(bucket.Count),
            GetText((long)bucket.Key),
            $"0x{bucket.Row:X}",
            .. columns.Skip(4).Select(c => fields.FirstOrDefault(f => f.Name == c.Header).Value ?? string.Empty)
        ];

        model.Entries.Add(new HashEntryModel
        {
            Cells = CreateCells(columns, values),
            IsCurrent = isCurrent && loaded.Access is { IsInsert: true },
            IsMatched = isCurrent && loaded.Access is { IsInsert: false, IsMatch: true }
        });

        for (var row = 1; row < bucket.Count; row++)
        {
            string[] unknown = [GetText(bucket.Index), string.Empty, UnknownValue, UnknownValue];

            model.Entries.Add(new HashEntryModel { Cells = CreateCells(columns, unknown), IsPlaceholder = true });
        }

        return model;
    }

    private static HashBucketModel CreateEmptyBucket(List<HashColumnModel> columns, int index)
    {
        var model = new HashBucketModel { Index = index, IsCurrent = true };

        model.Entries.Add(new HashEntryModel { Cells = CreateCells(columns, [GetText(index), GetText(0)]), IsPlaceholder = true });

        return model;
    }

    private static List<HashCellModel> CreateCells(List<HashColumnModel> columns, string[] values)
        => [.. columns.Select((c, i) => new HashCellModel { Column = c, Value = i < values.Length ? values[i] : string.Empty })];

    private sealed record LoadedTable(TraceHashTable Table,
                                      TraceHashAccess? Access,
                                      (int Partition, int Bucket)? Location,
                                      Dictionary<ulong, IReadOnlyList<(string Name, string Value)>> Rows);
}
