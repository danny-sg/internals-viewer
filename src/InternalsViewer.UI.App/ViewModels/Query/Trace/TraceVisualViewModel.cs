using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Allocations;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Allocation;
using AllocationUnit = InternalsViewer.Internals.Engine.Database.AllocationUnit;
using InternalsViewer.Execution.Iterators.BatchMode.DataAccess;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;
using System.IO;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.UI.App.Models.Query.Trace.Columnstore;
using InternalsViewer.Internals.Columnstore.Services;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceVisualViewModel(TraceVisualType visualType,
                                                 DatabaseSource database,
                                                 AllocationUnit allocationUnit,
                                                 IndexService indexService,
                                                 string title,
                                                 int nodeId = 0) : ObservableObject
{
    private const int MaxDrawnRuns = 1024;

    private readonly List<PageSpan> _visitedPages = [];

    /// <summary>
    /// The span already held for a page, so a page read again is recoloured rather than added a second time
    /// </summary>
    /// <remarks>
    /// A walk rereads the same pages endlessly - every descent of a correlated seek starts at the root - and a span per read would grow
    /// without limit while showing nothing a single span for that page does not. The whole list is copied on each read and walked again on
    /// each repaint, so the duplicates cost time quadratic in the length of the walk.
    /// </remarks>
    private readonly Dictionary<PageAddress, PageSpan> _visitedByAddress = [];

    private Color? _objectColour;

    private PageAddress? _currentTracePage;

    private AllocationBorder? _objectBorder;

    private bool _objectBorderVisible;

    [ObservableProperty]
    private List<IndexNode> _nodes = [];

    [ObservableProperty]
    private bool _isVisualInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ProgressValue))]
    private int _loadedPageCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ProgressValue))]
    private int _loadedSegmentCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ProgressMaximum))]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    private int _segmentCount;

    [ObservableProperty]
    private float _zoom = 1;

    [ObservableProperty]
    private bool _isZoomToFit = true;

    /// <summary>
    /// Whether the visual zooms in on the page the operator is reading rather than showing the whole structure
    /// </summary>
    [ObservableProperty]
    private bool _isZoomToPage;

    [ObservableProperty]
    private IReadOnlyList<PageSpan> _pageSpans = [];

    [ObservableProperty]
    private long _playheadTimeUs;

    [ObservableProperty]
    private PageAddress? _selectedPageAddress;

    [ObservableProperty]
    private int? _selectedSlot;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _allocationLayers = [];

    [ObservableProperty]
    private IReadOnlyList<AllocationBorder> _traceBorders = [];

    [ObservableProperty]
    private RowIdentifier? _selectedRowIdentifier;

    [ObservableProperty]
    private int _selectedRowSlotCount;

    [ObservableProperty]
    private IReadOnlyList<ScanRowGroup> _scanRowGroups = [];

    [ObservableProperty]
    private int? _activeRowGroupId;

    [ObservableProperty]
    private int _scanVersion;

    [ObservableProperty]
    private int _batchFirstRow;

    [ObservableProperty]
    private int _batchRowCount;

    public TraceVisualType VisualType { get; } = visualType;

    public string Title { get; } = title;

    public int NodeId { get; } = nodeId;

    public bool ShowObjectBorderImmediately { get; init; }

    public DatabaseSource Database { get; } = database;

    public AllocationUnit AllocationUnit { get; } = allocationUnit;

    public Color? OperatorColour { get; set; }

    public Color ObjectColour => OperatorColour ?? (_objectColour ??= AllocationLayerBuilder.GetObjectColour(Database, AllocationUnit));

    public long TotalPageCount => AllocationUnit.UsedPages;

    public string ProgressText => VisualType == TraceVisualType.Columnstore
        ? SegmentCount > 0 ? $"{LoadedSegmentCount:N0} of {SegmentCount:N0} segments" : string.Empty
        : TotalPageCount > 0 ? $"{LoadedPageCount:N0} of {TotalPageCount:N0} pages" : string.Empty;

    public bool IsProgressVisible => VisualType == TraceVisualType.Columnstore
        ? SegmentCount > 0
        : TotalPageCount >= IndexService.ProgressReportInterval;

    public double ProgressMaximum => VisualType == TraceVisualType.Columnstore ? SegmentCount : TotalPageCount;

    public double ProgressValue => VisualType == TraceVisualType.Columnstore ? LoadedSegmentCount : LoadedPageCount;

    public short VisualFileId
        => (AllocationUnit.FirstPage != PageAddress.Empty ? AllocationUnit.FirstPage : AllocationUnit.FirstIamPage).FileId;

    public int ExtentCount => Database.GetFilePageCount(VisualFileId) / 8;

    public PfsChain? PfsChain => Database.Pfs.GetValueOrDefault(VisualFileId);

    private IndexService IndexService { get; } = indexService;

    private Color LightObjectColour => ColourHelpers.Lighten(ObjectColour);

    public async Task LoadVisualAsync()
    {
        if (IsVisualInitialized)
        {
            return;
        }

        if (VisualType == TraceVisualType.Columnstore)
        {
            await LoadColumnstoreAsync(CancellationToken.None);

            IsVisualInitialized = true;

            return;
        }

        if (VisualType == TraceVisualType.Allocation)
        {
            var layers = await Task.Run(() => AllocationLayerBuilder.GenerateLayers(Database, true, false, 20));

            var traceName = string.IsNullOrEmpty(AllocationUnit.IndexName)
                ? $"{AllocationUnit.SchemaName}.{AllocationUnit.TableName}"
                : $"{AllocationUnit.SchemaName}.{AllocationUnit.TableName}.{AllocationUnit.IndexName}";

            foreach (var layer in layers.Where(l => !l.IsAllocationLayer))
            {
                layer.Opacity = layer.Name == traceName ? (byte)80 : (byte)5;
            }

            AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

            var iamPageIds = AllocationUnit.IamChain
                                           .Pages
                                           .Where(p => p.PageAddress.FileId == VisualFileId)
                                           .Select(p => p.PageAddress.PageId)
                                           .ToHashSet();

            var ranges = AllocationUnit.IamChain
                                       .GetAllocatedPageRanges(VisualFileId)
                                       .Where(r => !(r.From == r.To && iamPageIds.Contains(r.From)))
                                       .Select(r => new TimedRange(r.From, r.To, 0, long.MaxValue))
                                       .ToList();

            _objectBorder = new AllocationBorder(AllocationBorderScope.Page, VisualFileId, Color.DimGray, ranges);

            if (ShowObjectBorderImmediately)
            {
                _objectBorderVisible = true;

                TraceBorders = [_objectBorder];
            }

            IsVisualInitialized = true;

            return;
        }

        LoadedPageCount = 0;

        IndexService.ProgressReportInterval = TotalPageCount > 100_000 ? 4096 : 1;

        OnPropertyChanged(nameof(IsProgressVisible));

        var progress = new Progress<int>(count => LoadedPageCount = count);

        Nodes = await Task.Run(() => IndexService.GetNodes(Database, AllocationUnit.RootPage, CancellationToken.None, progress));

        IsVisualInitialized = true;
    }

    public void Apply(AccessStep step)
    {
        if (step.NodeId != NodeId)
        {
            return;
        }

        if (VisualType == TraceVisualType.Columnstore)
        {
            ApplyColumnstore(step);

            return;
        }

        if (step is AccessStep.Close)
        {
            ClearSelection();

            return;
        }

        if (VisualType == TraceVisualType.Index)
        {
            if (step is AccessStep.Reseek or AccessStep.Rebind)
            {
                LightenVisitedPages(_visitedPages);

                PageSpans = [.. _visitedPages];
            }
            else if (step is AccessStep.ReadPage read)
            {
                SelectedPageAddress = read.PageAddress;

                SelectedSlot = null;

                Visit(_visitedPages, _visitedByAddress, read.PageAddress);

                PageSpans = [.. _visitedPages];
            }
            else
            {
                SelectedSlot = GetStepSlot(step) ?? SelectedSlot;
            }

            return;
        }

        switch (step)
        {
            case AccessStep.ReadPage read:
                _currentTracePage = read.PageAddress;
                SelectedRowSlotCount = read.SlotCount;
                SelectedRowIdentifier = null;
                break;

            case AccessStep.Row row when _currentTracePage is { } rowPage:
                SelectedRowIdentifier = new RowIdentifier(rowPage, (ushort)row.Slot);
                break;

            case AccessStep.RowRun run when _currentTracePage is { } runPage:
                SelectedRowIdentifier = new RowIdentifier(runPage, (ushort)run.ToSlot);
                break;

            case AccessStep.IamRead:
                _objectBorderVisible = true;
                SelectedRowIdentifier = null;
                TraceBorders = _objectBorder is { } revealed ? [revealed] : [];
                break;

            default:
                SelectedRowIdentifier = null;
                break;
        }

        var current = step switch
        {
            AccessStep.ReadPage readPage => readPage.PageAddress,
            AccessStep.PageSkipped skipped => skipped.PageAddress,
            AccessStep.PfsRead pfsRead => pfsRead.PageAddress,
            _ => (PageAddress?)null
        };

        if (current is { } page)
        {
            SelectedPageAddress = page;

            SetTraceBorders(page);
        }
    }

    public TraceVisualReplay ComputeReplay(IReadOnlyList<AccessStep> steps)
    {
        var visited = new List<PageSpan>();

        var visitedByAddress = new Dictionary<PageAddress, PageSpan>();

        PageAddress? lastPage = null;

        PageAddress? lastDataPage = null;

        int? lastSlot = null;

        var lastSlotCount = 0;

        foreach (var step in steps)
        {
            if (step.NodeId != NodeId)
            {
                continue;
            }

            switch (step)
            {
                case AccessStep.Reseek or AccessStep.Rebind when VisualType == TraceVisualType.Index:
                    LightenVisitedPages(visited);
                    break;

                case AccessStep.ReadPage read:
                    Visit(visited, visitedByAddress, read.PageAddress);
                    lastPage = read.PageAddress;
                    lastDataPage = read.PageAddress;
                    lastSlotCount = read.SlotCount;
                    lastSlot = null;
                    break;

                case AccessStep.PageSkipped skipped when VisualType == TraceVisualType.Allocation:
                    lastPage = skipped.PageAddress;
                    break;

                case AccessStep.PfsRead pfsRead when VisualType == TraceVisualType.Allocation:
                    lastPage = pfsRead.PageAddress;
                    break;

                case AccessStep.Close:
                    lastPage = null;
                    lastDataPage = null;
                    lastSlot = null;
                    lastSlotCount = 0;
                    break;

                default:
                    lastSlot = GetStepSlot(step) ?? lastSlot;
                    break;
            }
        }

        return new TraceVisualReplay(visited, lastPage, lastDataPage, lastSlot, lastSlotCount);
    }

    public async Task LoadColumnstoreAsync(CancellationToken cancellationToken)
    {
        var service = App.GetService<ColumnstoreService>();

        var index = await service.GetIndex(AllocationUnit, Database, cancellationToken);

        ScanRowGroups = [.. index.CompressedRowGroups
                                 .OrderBy(r => r.RowGroupId)
                                 .Select(r => new ScanRowGroup
                                 {
                                     RowGroupId = r.RowGroupId,
                                     TotalRows = r.TotalRows,
                                     Segments = [.. r.Segments
                                                     .Where(s => s.Column is { IsInternal: false })
                                                     .OrderBy(s => s.Column!.ColumnStoreColumnId)
                                                     .Select(s => new ScanSegment
                                                     {
                                                         ColumnId = s.Column!.ColumnStoreColumnId,
                                                         ColumnName = s.Column!.Name
                                                     })]
                                 })];

        await LoadSegmentRunsAsync(index, cancellationToken);
    }

    private async Task LoadSegmentRunsAsync(ColumnStoreIndex index, CancellationToken cancellationToken)
    {
        var service = App.GetService<ColumnstoreService>();

        var segments = index.CompressedRowGroups
                            .SelectMany(r => r.Segments.Where(s => s.Column is { IsInternal: false }))
                            .Count();

        LoadedSegmentCount = 0;

        SegmentCount = segments;

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            foreach (var segment in rowGroup.Segments.Where(s => s.Column is { IsInternal: false }))
            {
                try
                {
                    var blob = await service.GetSegmentBlob(Database,
                                                            segment,
                                                            cancellationToken,
                                                            depth: SegmentLoadDepth.Runs);

                    SetSegment(rowGroup.RowGroupId,
                               segment.Column!.ColumnStoreColumnId,
                               s => s.Runs = RunRows(blob));
                }
                catch (InvalidDataException)
                {
                }

                LoadedSegmentCount++;
            }
        }

        SegmentCount = 0;
    }

    public void ResetColumnstore()
    {
        if (VisualType != TraceVisualType.Columnstore)
        {
            return;
        }

        ActiveRowGroupId = null;

        BatchFirstRow = 0;

        BatchRowCount = 0;

        foreach (var rowGroup in ScanRowGroups)
        {
            rowGroup.IsEliminated = false;

            rowGroup.IsVisited = false;

            foreach (var segment in rowGroup.Segments)
            {
                segment.IsOpened = false;

                segment.IsEliminated = false;

                segment.IsProjected = false;
            }
        }

        ScanVersion++;
    }

    private void ApplyColumnstore(AccessStep step)
    {
        switch (step)
        {
            case AccessStep.Open:
                ResetColumnstore();
                break;

            case AccessStep.Close:
                ActiveRowGroupId = null;
                BatchFirstRow = 0;
                BatchRowCount = 0;
                break;

            case AccessStep.RowGroupOpened opened:
                ActiveRowGroupId = opened.RowGroupId;
                break;

            case AccessStep.SegmentOpened segmentOpened:
                SetSegment(segmentOpened.RowGroupId, segmentOpened.ColumnId, s => s.IsOpened = true);
                break;

            case AccessStep.SegmentSkipped segmentSkipped:
                SetSegment(segmentSkipped.RowGroupId, segmentSkipped.ColumnId, s => s.IsEliminated = true);
                break;

            case AccessStep.RowGroupSkipped rowGroupSkipped:
                SetRowGroup(rowGroupSkipped.RowGroupId, r => r.IsEliminated = true);
                break;

            case AccessStep.BatchProduced batch:
                ActiveRowGroupId = batch.RowGroupId;
                BatchFirstRow = batch.FirstRow;
                BatchRowCount = batch.RowCount;
                SetRowGroup(batch.RowGroupId, r => r.IsVisited = true);
                break;
        }
    }

    private static IReadOnlyList<int> RunRows(SegmentBlob blob)
    {
        var runs = new List<int>();

        foreach (var run in new SegmentDataIdStream(blob).GetRuns(0, blob.RowCount))
        {
            runs.Add(run.RowCount);
        }

        if (runs.Count <= MaxDrawnRuns)
        {
            return runs;
        }

        var merge = (runs.Count + MaxDrawnRuns - 1) / MaxDrawnRuns;

        var merged = new List<int>(MaxDrawnRuns);

        for (var i = 0; i < runs.Count; i += merge)
        {
            var rows = 0;

            for (var j = i; j < runs.Count && j < i + merge; j++)
            {
                rows += runs[j];
            }

            merged.Add(rows);
        }

        return merged;
    }

    private void SetRowGroup(int rowGroupId, Action<ScanRowGroup> apply)
    {
        foreach (var rowGroup in ScanRowGroups.Where(r => r.RowGroupId == rowGroupId))
        {
            apply(rowGroup);
        }

        ScanVersion++;
    }

    private void SetSegment(int rowGroupId, int columnId, Action<ScanSegment> apply)
    {
        foreach (var segment in ScanRowGroups.Where(r => r.RowGroupId == rowGroupId)
                                             .SelectMany(r => r.Segments)
                                             .Where(s => s.ColumnId == columnId))
        {
            segment.IsProjected = true;

            apply(segment);
        }

        ScanVersion++;
    }

    public void ApplyReplay(TraceVisualReplay replay)
    {
        if (VisualType == TraceVisualType.Index)
        {
            _visitedPages.Clear();
            _visitedPages.AddRange(replay.Visited);

            _visitedByAddress.Clear();

            foreach (var span in _visitedPages)
            {
                _visitedByAddress[span.Address] = span;
            }

            PageSpans = [.. _visitedPages];
            SelectedPageAddress = replay.LastPage;
            SelectedSlot = replay.LastSlot;

            return;
        }

        if (replay.LastPage is { } page)
        {
            _objectBorderVisible = true;

            SelectedPageAddress = page;

            SetTraceBorders(page);

            _currentTracePage = replay.LastDataPage;
            SelectedRowSlotCount = replay.LastSlotCount;
            SelectedRowIdentifier = replay is { LastDataPage: { } dataPage, LastSlot: { } slot }
                                    ? new RowIdentifier(dataPage, (ushort)slot)
                                    : null;
        }
    }

    public void Reset()
    {
        _visitedPages.Clear();
        _visitedByAddress.Clear();
        _currentTracePage = null;
        _objectBorderVisible = false;
        PageSpans = [];
        SelectedPageAddress = null;
        SelectedSlot = null;
        SelectedRowIdentifier = null;
        SelectedRowSlotCount = 0;
        TraceBorders = [];
    }

    internal static IndexRecordModel ToRecordModel(IRecord record, RecordColumnFilter? columns = null)
    {
        var rowIdentifier = GetRowIdentifier(record);

        var fields = (columns ?? RecordColumnFilter.All).Apply(record.Fields);

        return new IndexRecordModel
        {
            Slot = record.Slot,
            RowIdentifier = rowIdentifier,
            Fields =
            [
                .. fields.Select(f => new IndexRecordFieldModel
                {
                    Name = f.Name,
                    Value = ValueOf(f, rowIdentifier),
                    DataType = f.ColumnStructure.DataType
                })
            ]
        };
    }

    /// <summary>
    /// The row identifier a record carries, which only the record formats that hold one expose
    /// </summary>
    /// <remarks>
    /// A nonclustered index of a heap stores the row identifier of the row it points at, and a heap row knows its own. Neither is on
    /// <see cref="IRecord"/>, so the format has to be asked.
    /// </remarks>
    internal static RowIdentifier? GetRowIdentifier(IRecord record)
        => record switch
        {
            FixedVarIndexRecord index => index.Rid,
            CdIndexRecord cdIndex => cdIndex.Rid,
            DataRecord data => data.RowIdentifier,
            CdRecord cd => cd.RowIdentifier,
            _ => null
        };

    /// <summary>
    /// Drops what the walk was pointing at, keeping the pages it visited
    /// </summary>
    /// <remarks>
    /// A closed operator holds no position, so nothing should be lit as current. What it read on the way is history rather than a
    /// position, so the visited pages stay.
    /// </remarks>
    private void ClearSelection()
    {
        SelectedPageAddress = null;
        SelectedSlot = null;
        SelectedRowIdentifier = null;

        _currentTracePage = null;
    }

    /// <summary>
    /// The text a field shows, taking the row identifier from the record for the hidden column that holds it
    /// </summary>
    /// <remarks>
    /// A nonclustered index of a heap stores the row identifier in a hidden column, which the record loader reads onto the record itself
    /// and leaves as an empty field, so the value has to be put back for display.
    /// </remarks>
    private static string ValueOf(RecordField field, RowIdentifier? rowIdentifier)
        => field.ColumnStructure is IndexColumnStructure { IsRowIdentifier: true }
            ? rowIdentifier?.ToString() ?? field.Value
            : field.Value;

    /// <summary>
    /// Records a page as visited, or brings back to full colour one already held
    /// </summary>
    private void Visit(List<PageSpan> spans, Dictionary<PageAddress, PageSpan> byAddress, PageAddress page)
    {
        if (byAddress.TryGetValue(page, out var visited))
        {
            visited.DisplayColour = ObjectColour;

            return;
        }

        var span = new PageSpan(page, 0, long.MaxValue, ObjectColour);

        spans.Add(span);

        byAddress[page] = span;
    }

    private void LightenVisitedPages(List<PageSpan> spans)
    {
        var light = LightObjectColour;

        foreach (var span in spans)
        {
            span.DisplayColour = light;
        }
    }

    private static int? GetStepSlot(AccessStep step)
    {
        return step switch
        {
            AccessStep.ReadPage => null,
            AccessStep.Probe probe => probe.Middle,
            AccessStep.ProbeResult probeResult => probeResult.Slot,
            AccessStep.Row row => row.Slot,
            AccessStep.RowRun run => run.ToSlot,
            AccessStep.RangeEnd rangeEnd => rangeEnd.Slot,
            AccessStep.Descend descend => descend.Slot,
            _ => null
        };
    }

    private void SetTraceBorders(PageAddress page)
    {
        var currentBorder = new AllocationBorder(AllocationBorderScope.Page,
                                                 page.FileId,
                                                 Color.Red,
                                                 [new TimedRange(page.PageId, page.PageId, 0, long.MaxValue)]);

        TraceBorders = _objectBorderVisible && _objectBorder is { } border ? [border, currentBorder] : [currentBorder];
    }
}
