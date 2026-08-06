using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Trace;

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

    [ObservableProperty]
    private IReadOnlyList<HashBucketModel> _buckets = [];

    [ObservableProperty]
    private IReadOnlyList<HashColumnModel> _columns = HashColumnModel.CreateBaseColumns();

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private int _bucketCount = 1 << JoinHash.DefaultBucketBits;

    private IHashTableIterator? Iterator { get; set; }

    private List<HashBucketModel>? _bucketModels;

    private int _syncedRowCount;

    private HashBucketModel? _currentBucket;

    private HashEntryModel? _currentEntry;

    private readonly List<HashEntryModel> _matchedEntries = [];

    private bool _suppressResize;

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

    partial void OnBucketCountChanged(int value)
    {
        if (_suppressResize || Iterator is not { } iterator)
        {
            return;
        }

        iterator.SetBucketCount(value);

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
            && step is AccessStep.HashBuild { IsNullKey: false } build)
        {
            models[build.Bucket].Entries.Add(ToEntryModel(table.Buckets[build.Bucket].Entries[build.Entry],
                                                          build.Bucket,
                                                          build.Entry));

            _syncedRowCount = table.RowCount;
        }
        else if (_bucketModels is null
                 || _bucketModels.Count != table.BucketCount
                 || _syncedRowCount != table.RowCount)
        {
            RebuildBuckets(table);

            Buckets = _bucketModels!;
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
        _currentBucket = null;
        _currentEntry = null;

        _matchedEntries.Clear();

        Buckets = [];
        Columns = HashColumnModel.CreateBaseColumns();
        Summary = string.Empty;
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

            foreach (var entry in bucket.Entries)
            {
                model.Entries.Add(ToEntryModel(entry, bucket.Index, model.Entries.Count));
            }

            models.Add(model);
        }

        _bucketModels = models;
        _syncedRowCount = table.RowCount;
    }

    private void UpdateHighlight(AccessStep? step)
    {
        // A new probe row starts a fresh verdict, so whatever the last one matched stops being green
        if (step is AccessStep.HashProbe or AccessStep.HashBuild)
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
