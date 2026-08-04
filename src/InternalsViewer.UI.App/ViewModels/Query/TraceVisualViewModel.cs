using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Trace;
using System.Drawing;
using AllocationBorder = InternalsViewer.UI.App.Models.AllocationBorder;
using AllocationBorderScope = InternalsViewer.UI.App.Models.AllocationBorderScope;
using AllocationLayer = InternalsViewer.UI.App.Models.AllocationLayer;
using TimedRange = InternalsViewer.UI.App.Models.TimedRange;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;

namespace InternalsViewer.UI.App.ViewModels.Query;

public enum TraceVisualKind
{
    Index,
    Allocation
}

public sealed partial class TraceVisualViewModel(TraceVisualKind kind,
                                                 DatabaseSource database,
                                                 AllocationUnit allocationUnit,
                                                 IndexService indexService,
                                                 string title,
                                                 int source = 0) : ObservableObject
{
    public TraceVisualKind Kind { get; } = kind;

    public string Title { get; } = title;

    public int Source { get; } = source;

    public bool IsSideStackVisible { get; init; }

    /// <summary>
    /// Shows the hash table in place of the side record stack, for the build input of a hash match
    /// </summary>
    public bool IsHashTableVisible { get; init; }

    /// <summary>
    /// Which input of the join this side is, 0 for outer or build and 1 for inner or probe
    /// </summary>
    /// <remarks>
    /// Source identifies the operator and comes from the plan, so it can no longer say which side of its parent an input sits on.
    /// </remarks>
    public int InputIndex { get; init; } = -1;

    /// <summary>
    /// The operator this input feeds, which with <see cref="InputIndex"/> names the buffer holding its rows
    /// </summary>
    public int OperatorNodeId { get; init; }

    /// <summary>
    /// The columns this side's records show, taken from what its operator outputs
    /// </summary>
    public RecordColumnFilter ColumnFilter { get; init; } = RecordColumnFilter.All;

    /// <summary>
    /// Outlines the object as soon as the map loads, for a path that never reads the IAM chain
    /// </summary>
    public bool ShowObjectBorderImmediately { get; init; }

    public DatabaseSource Database { get; } = database;

    public AllocationUnit AllocationUnit { get; } = allocationUnit;

    private IndexService IndexService { get; } = indexService;

    [ObservableProperty]
    private List<IndexNode> _nodes = [];

    [ObservableProperty]
    private bool _isVisualInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _loadedPageCount;

    [ObservableProperty]
    private float _zoom = 1;

    [ObservableProperty]
    private bool _isZoomToFit = true;

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
    private bool _isDimmed;

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _sideRecords = [];

    private Color? _objectColour;

    public Color ObjectColour => _objectColour ??= AllocationLayerBuilder.GetObjectColour(Database, AllocationUnit);

    private Color LightObjectColour => Lighten(ObjectColour);

    private PageAddress? _currentTracePage;

    private AllocationBorder? _objectBorder;

    private bool _objectBorderVisible;

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

    public long TotalPageCount => AllocationUnit.UsedPages;

    public string ProgressText => TotalPageCount > 0 ? $"{LoadedPageCount:N0} of {TotalPageCount:N0} pages" : string.Empty;

    public bool IsProgressVisible => TotalPageCount >= IndexService.ProgressReportInterval;

    public double ProgressMaximum => TotalPageCount;

    public short VisualFileId
        => (AllocationUnit.FirstPage != PageAddress.Empty ? AllocationUnit.FirstPage : AllocationUnit.FirstIamPage).FileId;

    public int ExtentCount => Database.GetFilePageCount(VisualFileId) / 8;

    public PfsChain? PfsChain => Database.Pfs.GetValueOrDefault(VisualFileId);

    public async Task LoadVisualAsync()
    {
        if (IsVisualInitialized)
        {
            return;
        }

        if (Kind == TraceVisualKind.Allocation)
        {
            var layers = await Task.Run(() => AllocationLayerBuilder.GenerateLayers(Database, true, 20));

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
        if (step.Source != Source)
        {
            return;
        }

        if (Kind == TraceVisualKind.Index)
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
            if (step.Source != Source)
            {
                continue;
            }

            switch (step)
            {
                case AccessStep.Reseek or AccessStep.Rebind when Kind == TraceVisualKind.Index:
                    LightenVisitedPages(visited);
                    break;

                case AccessStep.ReadPage read:
                    Visit(visited, visitedByAddress, read.PageAddress);
                    lastPage = read.PageAddress;
                    lastDataPage = read.PageAddress;
                    lastSlotCount = read.SlotCount;
                    lastSlot = null;
                    break;

                case AccessStep.PageSkipped skipped when Kind == TraceVisualKind.Allocation:
                    lastPage = skipped.PageAddress;
                    break;

                case AccessStep.PfsRead pfsRead when Kind == TraceVisualKind.Allocation:
                    lastPage = pfsRead.PageAddress;
                    break;

                default:
                    lastSlot = GetStepSlot(step) ?? lastSlot;
                    break;
            }
        }

        return new TraceVisualReplay(visited, lastPage, lastDataPage, lastSlot, lastSlotCount);
    }

    public void ApplyReplay(TraceVisualReplay replay)
    {
        if (Kind == TraceVisualKind.Index)
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

            SetTraceBorders(page);

            _currentTracePage = replay.LastDataPage;
            SelectedRowSlotCount = replay.LastSlotCount;
            SelectedRowIdentifier = replay.LastDataPage is { } dataPage && replay.LastSlot is { } slot
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
        IsDimmed = false;
        SideRecords = [];
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

    private static Color Lighten(Color colour)
    {
        return Color.FromArgb(255,
                              colour.R + (255 - colour.R) * 3 / 4,
                              colour.G + (255 - colour.G) * 3 / 4,
                              colour.B + (255 - colour.B) * 3 / 4);
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

public sealed record TraceVisualReplay(List<PageSpan> Visited,
                                       PageAddress? LastPage,
                                       PageAddress? LastDataPage,
                                       int? LastSlot,
                                       int LastSlotCount);
