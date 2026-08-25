using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Models.Query.Trace.Hash;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

/// <summary>
/// The hash table one hash match fills, and what its walk is currently doing to it
/// </summary>
/// <remarks>
/// A table belongs to the operator that built it rather than to the object its build side read. A hash match reading another operator has
/// no object to hang it on, and a trace holding two hash matches has two tables that would otherwise be written into one another.
/// </remarks>
public sealed partial class TraceHashTableViewModel(RecordColumnFilter columnFilter) : ObservableObject
{
    private const int BucketColumn = 0;

    private const int HashColumn = 1;

    private const int FirstValueColumn = 2;

    private readonly List<HashEntryModel> _matchedEntries = [];

    private List<HashBucketModel>? _bucketModels;

    private int _syncedRowCount;

    private HashBucketModel? _currentBucket;

    private HashEntryModel? _currentEntry;

    private bool _suppressResize;

    private IReadOnlyList<HashColumnModel>? _placeholderColumns;

    [ObservableProperty]
    private IReadOnlyList<HashBucketModel> _buckets = [];

    [ObservableProperty]
    private IReadOnlyList<HashColumnModel> _columns = HashColumnModel.CreateBaseColumns();

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private int _bucketCount = 1 << JoinHash.DefaultBucketBits;

    private IHashTableIterator? Iterator { get; set; }

    /// <summary>
    /// Binds to the iterator that fills this table, which is a new one each time the trace is opened
    /// </summary>
    public void Attach(IHashTableIterator iterator)
    {
        Iterator = iterator;

        _suppressResize = true;

        BucketCount = iterator.Table.BucketCount;

        _suppressResize = false;

        Sync(null);
    }

    /// <summary>
    /// Brings the table up to date with the step just taken, which only changes it when the step is this operator's own
    /// </summary>
    /// <remarks>
    /// A nested hash match's steps flow up through the operator above it, so a step is matched on the id it carries rather than on its
    /// kind. Matching on kind alone shows one operator's build in the other's table.
    /// </remarks>
    public void Sync(AccessStep? step)
    {
        if (Iterator is not { } iterator)
        {
            return;
        }

        var table = iterator.Table;

        var isOwnStep = step is not null && step.NodeId == iterator.NodeId;

        if (isOwnStep
            && _bucketModels is { } models
            && models.Count == table.BucketCount
            && table.RowCount == _syncedRowCount + 1
            && step != null
            && Added(step) is { } added)
        {
            var entries = models[added.Bucket].Entries;

            var entry = ToEntryModel(table.Buckets[added.Bucket].Entries[added.Entry], added.Bucket, added.Entry);

            if (entries is [{ IsPlaceholder: true }])
            {
                entries.Reset([entry]);
            }
            else
            {
                entries.Add(entry);
            }

            _syncedRowCount = table.RowCount;
        }
        else if (_bucketModels is null
                 || _bucketModels.Count != table.BucketCount
                 || _syncedRowCount != table.RowCount)
        {
            RebuildBuckets(table);

            Buckets = _bucketModels!;
        }

        if (!ReferenceEquals(_placeholderColumns, Columns))
        {
            _placeholderColumns = Columns;

            RefreshEmptyBuckets();
        }

        if (isOwnStep && step is AccessStep.HashAggregate { IsNewGroup: false } folded)
        {
            RefreshEntry(table, folded.Bucket, folded.Entry);
        }

        if (isOwnStep)
        {
            UpdateHighlight(step);
        }

        Summary = iterator.BuildRowEstimate > 0
            ? $"{table.RowCount:N0} rows, sized for {iterator.BuildRowEstimate:N0}, "
              + $"{table.BucketCount} buckets, longest chain {table.LongestChain}"
            : $"{table.RowCount:N0} rows, {table.BucketCount} buckets, longest chain {table.LongestChain}";
    }

    public void Reset()
    {
        Iterator = null;

        _bucketModels = null;
        _syncedRowCount = 0;
        _placeholderColumns = null;
        _currentBucket = null;
        _currentEntry = null;

        _matchedEntries.Clear();

        Buckets = [];
        Columns = HashColumnModel.CreateBaseColumns();
        Summary = string.Empty;
    }

    partial void OnBucketCountChanged(int value)
    {
        if (_suppressResize || Iterator is not { } iterator)
        {
            return;
        }

        iterator.SetBucketCount(value);

        Sync(null);
    }

    private static (int Bucket, int Entry)? Added(AccessStep step)
        => step switch
        {
            AccessStep.HashBuild { IsNullKey: false } build => (build.Bucket, build.Entry),
            AccessStep.HashAggregate { IsNewGroup: true } group => (group.Bucket, group.Entry),
            _ => null
        };

    private void RefreshEntry(HashTable table, int bucket, int entry)
    {
        if (_bucketModels is not { } models
            || bucket < 0
            || bucket >= models.Count
            || entry < 0
            || entry >= models[bucket].Entries.Count
            || entry >= table.Buckets[bucket].Count)
        {
            return;
        }

        models[bucket].Entries[entry] = ToEntryModel(table.Buckets[bucket].Entries[entry], bucket, entry);
    }

    private void RebuildBuckets(HashTable table)
    {
        _currentBucket = null;
        _currentEntry = null;

        _matchedEntries.Clear();

        var models = new List<HashBucketModel>(table.BucketCount);

        foreach (var bucket in table.Buckets)
        {
            var model = new HashBucketModel { Index = bucket.Index };

            var entries = new List<HashEntryModel>(bucket.Count);

            foreach (var entry in bucket.Entries)
            {
                entries.Add(ToEntryModel(entry, bucket.Index, entries.Count));
            }

            if (entries.Count == 0)
            {
                entries.Add(EmptyEntryModel(bucket.Index));
            }

            model.Entries.Reset(entries);

            models.Add(model);
        }

        _bucketModels = models;
        _syncedRowCount = table.RowCount;
    }

    private void UpdateHighlight(AccessStep? step)
    {
        // A new probe row starts a fresh verdict, so whatever the last one matched stops being green
        if (step is AccessStep.HashProbe or AccessStep.HashBuild or AccessStep.HashAggregate)
        {
            ClearMatchedEntries();
        }

        _currentBucket?.IsCurrent = false;
        _currentBucket = null;

        _currentEntry?.IsCurrent = false;
        _currentEntry = null;

        var (bucketIndex, entryIndex) = step switch
        {
            AccessStep.HashBuild build => (build.Bucket, build.Entry),
            AccessStep.HashProbe probe => (probe.Bucket, -1),
            AccessStep.HashCompare compare => (compare.Bucket, compare.Entry),
            AccessStep.HashAggregate group => (group.Bucket, group.Entry),
            _ => (-1, -1)
        };

        if (_bucketModels is not { } models || bucketIndex < 0 || bucketIndex >= models.Count)
        {
            return;
        }

        _currentBucket = models[bucketIndex];
        _currentBucket.IsCurrent = true;

        if (entryIndex < 0 || entryIndex >= _currentBucket.Entries.Count)
        {
            return;
        }

        _currentEntry = _currentBucket.Entries[entryIndex];
        _currentEntry.IsCurrent = true;

        // The table's own matched flag stays set for the outer join drain, so the green here follows this comparison alone
        if (step is AccessStep.HashCompare { IsMatch: true })
        {
            _currentEntry.IsMatched = true;

            _matchedEntries.Add(_currentEntry);
        }
    }

    private void ClearMatchedEntries()
    {
        foreach (var entry in _matchedEntries)
        {
            entry.IsMatched = false;
        }

        _matchedEntries.Clear();
    }

    private HashEntryModel EmptyEntryModel(int bucketIndex)
    {
        var columns = Columns;

        var cells = new List<HashCellModel>(columns.Count)
        {
            new() { Value = bucketIndex.ToString(), Column = columns[BucketColumn] }
        };

        for (var index = BucketColumn + 1; index < columns.Count; index++)
        {
            cells.Add(new HashCellModel { Column = columns[index] });
        }

        return new HashEntryModel { IsPlaceholder = true, Cells = cells };
    }

    private void RefreshEmptyBuckets()
    {
        if (_bucketModels is not { } models)
        {
            return;
        }

        foreach (var model in models)
        {
            if (model.Entries is [{ IsPlaceholder: true }])
            {
                model.Entries[0] = EmptyEntryModel(model.Index);
            }
        }
    }

    private HashEntryModel ToEntryModel(HashEntry entry, int bucketIndex, int entryIndex)
    {
        var record = TraceVisualViewModel.ToRecordModel(entry.Record, columnFilter);

        var columns = EnsureColumns(record);

        var cells = new List<HashCellModel>(columns.Count)
        {
            // Only the row that opens a bucket names it, so a chain reads as one bucket rather than the number repeating
            new() { Value = entryIndex == 0 ? bucketIndex.ToString() : string.Empty, Column = columns[BucketColumn] },
            new() { Value = $"{entry.Hash:X8}", Column = columns[HashColumn] }
        };

        // Positional, because a build side reading two objects can carry the same column name twice and a lookup would find only the first
        for (var index = FirstValueColumn; index < columns.Count; index++)
        {
            var field = index - FirstValueColumn < record.Fields.Count ? record.Fields[index - FirstValueColumn] : null;

            cells.Add(new HashCellModel { Value = field?.Value ?? string.Empty, Column = columns[index] });
        }

        return new HashEntryModel { Cells = cells };
    }

    /// <summary>
    /// Widens the grid to the columns a build row actually carries, which are not known until the first row is read
    /// </summary>
    /// <remarks>
    /// The base columns exist so the header is there before the build starts. Every row of a given build side carries the same columns, so
    /// this settles on the first row and the rest line up under it.
    /// </remarks>
    private IReadOnlyList<HashColumnModel> EnsureColumns(IndexRecordModel record)
    {
        if (Columns.Count > FirstValueColumn)
        {
            return Columns;
        }

        var columns = new List<HashColumnModel>(HashColumnModel.CreateBaseColumns());

        columns.AddRange(record.Fields.Select(f => new HashColumnModel { Header = f.Name }));

        Columns = columns;

        return columns;
    }
}
