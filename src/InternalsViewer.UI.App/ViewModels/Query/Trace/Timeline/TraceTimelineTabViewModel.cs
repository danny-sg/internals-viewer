using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;
using InternalsViewer.Query.Recording;
using InternalsViewer.UI.App.Controls.Query.Trace.Calls;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;
using InternalsViewer.UI.Plugins.Documents;
using InternalsViewer.UI.Plugins.FullTrace;
using InternalsViewer.UI.Plugins.FullTrace.Menus;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public sealed partial class TraceTimelineTabViewModel : ObservableObject, ITraceNavigator, ITracePluginHost, IDisposable
{
    private const double ChartStar = 2;

    private const double PanelStar = 1;

    private const double CommandSpacing = 2;

    private const string StepBackGlyph = "";

    private const string StepForwardGlyph = "";

    [ObservableProperty]
    private bool _isCallStackVisible;

    public TraceTimelineTabViewModel(QueryViewModel query)
    {
        Query = query;

        PlayheadStack = new PlayheadCallStackViewModel(query);

        CallStackDocument = new DocumentViewModel("Call Stack",
                                                  PlayheadStack,
                                                  static () => new CallStackControl(),
                                                  keepAlive: true,
                                                  key: "CallStack",
                                                  persist: false,
                                                  commandsFactory: CreateCallStackCommands);

        Dock = new DockLayoutViewModel(new FixedNode());

        Dock.DocumentClosed += OnDocumentClosed;

        Dock.LayoutChanged += OnDockLayoutChanged;

        Dock.SelectionChanged += OnDockSelectionChanged;

        Dock.DocumentActivated += OnDocumentActivated;

        Query.Layout.Dock.DocumentClosed += OnQueryDocumentClosed;
    }

    public event EventHandler<TracePoint>? PlayheadRequested;

    public event EventHandler<int?>? MemoryHighlightRequested;

    public DockLayoutViewModel Dock { get; }

    public PlayheadCallStackViewModel PlayheadStack { get; }

    public IPlayheadChart? Chart
    {
        get;
        set
        {
            field = value;

            PlayheadStack.Chart = value;

            value?.ShowMemoryUpdates((ShownMemory?.Content as PlayheadMemoryViewModel)?.GetUpdates(), ShownMemory?.Title);

            foreach (var document in MemoryDocuments)
            {
                ((PlayheadMemoryViewModel)document.Content).Chart = value;
            }
        }
    }

    public TracePosition? Playhead { get; private set; }

    public ITraceTimelineController? Controller { get; set; }

    public TraceViewRange? ViewRange => Chart?.GetViewRange();

    public TraceResult? Trace => Query.CurrentRun?.ExtraOf<TraceResult>();

    public TimeTravelTraceFile? TraceFile => Query.TraceFile;

    private QueryViewModel Query { get; }

    private DocumentViewModel CallStackDocument { get; }

    private List<DocumentViewModel> MemoryDocuments { get; } = [];

    private List<DocumentViewModel> GraphDocuments { get; } = [];

    private List<DocumentViewModel> StructureDocuments { get; } = [];

    private List<DocumentViewModel> PluginDocuments { get; } = [];

    private DocumentViewModel? LastActivatedMemory { get; set; }

    private DocumentViewModel? ShownMemory { get; set; }

    public void ShowCallStack() => IsCallStackVisible = true;

    public void CloseCallStack()
    {
        PlayheadStack.Clear();

        IsCallStackVisible = false;
    }

    public void RequestPlayhead(TracePoint point)
    {
        if (Query.IsOutsideCrop(point.Position))
        {
            Query.QueryOptions.CropToQuery = false;
        }

        PlayheadRequested?.Invoke(this, point);
    }

    public void MovePlayhead(TracePoint point) => RequestPlayhead(point);

    public void ZoomToCall(CallTreeNode node, int callIndex)
    {
        if (Query.IsOutsideCrop(node))
        {
            Query.QueryOptions.CropToQuery = false;
        }

        PlayheadStack.RequestChart(c => c.ZoomToCall(node, callIndex));
    }

    public void ShowCallStack(TracePoint point)
    {
        Query.Layout.ShowTraceTimeline();

        RequestPlayhead(point);

        ShowCallStack();

        Dock.Activate(CallStackDocument);
    }

    public void FindInCallTree(CallTreeNode node, int callIndex) => Query.OpenCallTree(node, callIndex);

    public void FindReferences(ulong value)
    {
        Query.Layout.ShowCallTree();

        Query.CallTreeTab.FindReferences(value);
    }

    public void Search(string text)
    {
        Query.Layout.ShowTraceTimeline();

        Controller?.Search(text);
    }

    public void OpenBufferPage(TraceBufferReference reference)
    {
        if (!Query.MemoryHistory.IsQueryDatabase(reference))
        {
            return;
        }

        if (reference.IsBuffer)
        {
            Query.OpenPage(reference.Page);
        }
        else
        {
            Query.OpenPageAtOffset(reference.Page, reference.Offset);
        }
    }

    public void OpenIteratorTab(int block, ulong address, TracePosition position, IReadOnlyList<string> tabs)
    {
        OpenMemory(block, address, position);

        if (MemoryDocuments.FirstOrDefault(d => d.Key == $"Memory{block}")?.Content is PlayheadMemoryViewModel memory)
        {
            memory.ShowIteratorTab(tabs);
        }
    }

    public void OpenMemory(int block, ulong address, TracePosition position, int length = 1)
    {
        Query.Layout.ShowTraceTimeline();

        var key = $"Memory{block}";

        if (MemoryDocuments.FirstOrDefault(d => d.Key == key) is { } existing)
        {
            Dock.Activate(existing);

            (existing.Content as PlayheadMemoryViewModel)?.ShowAddress(address, length);

            LastActivatedMemory = existing;

            ShowActiveMemoryUpdates();

            return;
        }

        var memory = new PlayheadMemoryViewModel(Query.MemoryHistory);

        if (!memory.Open(block, address, position))
        {
            return;
        }

        if (length > 1)
        {
            memory.ShowAddress(address, length);
        }

        memory.SetPlayhead(Query.IsOutsideCrop(position) ? null : Playhead);

        memory.Chart = Chart;

        memory.Navigator = this;

        memory.PlayheadRequested += OnMemoryPlayheadRequested;

        memory.MemoryRequested += OnMemoryRequested;

        memory.Opened += OnMemoryOpened;

        List<DocumentMenuItem> menuItems =
        [
            new("Open Reference Graph", () => OpenReferenceGraph(memory.Block, memory.Position)),
            new("Open Hash Table", memory.OpenHashTable, () => memory.IsHashTableVisible),
            new("Open Sort Table", memory.OpenSortTable, () => memory.IsSortTableVisible)
        ];

        var document = new DocumentViewModel(memory.Title,
                                             memory,
                                             static () => new TraceTimelineMemoryView(),
                                             keepAlive: true,
                                             key: key,
                                             persist: false,
                                             commandsFactory: () => CreateMemoryCommands(memory))
        {
            MinWidth = HexViewControl.StandardWidth,
            MenuItems = menuItems
        };

        var neighbour = MemoryDocuments.LastOrDefault(Dock.Contains) ?? CallStackDocument;

        MemoryDocuments.Add(document);

        ShowPanel(document, neighbour);

        LastActivatedMemory = document;

        ShowActiveMemoryUpdates();
    }

    public void HighlightMemory(int? block) => MemoryHighlightRequested?.Invoke(this, block);

    public void OpenReferenceGraph(int block, TracePosition position)
    {
        if (block < 0 || block >= Query.MemoryHistory.Store.Blocks.Count)
        {
            return;
        }

        var key = $"Graph:{block}";

        if (GraphDocuments.FirstOrDefault(d => d.Key == key) is { } existing)
        {
            ShowGraph(existing);

            return;
        }

        AddGraph(key, new MemoryReferenceGraphViewModel(Query.MemoryHistory, block, position, this));
    }

    public void OpenObjectGraph(TraceStartCall start)
    {
        var key = $"Graph:Call:{start.Call.FromPosition}";

        if (GraphDocuments.FirstOrDefault(d => d.Key == key) is { } existing)
        {
            ShowGraph(existing);

            return;
        }

        AddGraph(key, new MemoryReferenceGraphViewModel(Query.MemoryHistory, start, this));
    }

    public void OpenHashTable(ulong group, TraceHashPart? part)
    {
        OpenHashTable(group, -1, -1, isBelowPlan: false);

        if (part is { } found && FindHashTable(group)?.Content is MemoryHashTableViewModel table)
        {
            table.ShowPart(group, found);
        }
    }

    public void OpenHashTable(ulong group, int partition, int bucket, bool isBelowPlan)
    {
        if (FindHashTable(group) is not { } document)
        {
            var table = new MemoryHashTableViewModel(Query.MemoryHistory, group, Playhead ?? TracePosition.Unknown, this);

            table.SetPlayhead(Playhead);

            document = new DocumentViewModel(table.Title,
                                             table,
                                             static () => new MemoryStructureView(),
                                             keepAlive: true,
                                             key: table.Key,
                                             persist: false)
            {
                MinWidth = HexViewControl.StandardWidth
            };

            StructureDocuments.Add(document);
        }

        ShowStructure(document, isBelowPlan);

        if (bucket >= 0)
        {
            ((MemoryHashTableViewModel)document.Content).ShowBucket(group, partition, bucket);
        }
    }

    public void OpenSortTable(ulong iterator, TraceSortPart? part)
    {
        OpenSortTable(iterator, isBelowPlan: false);

        if (part is { } found && FindSortTable(iterator)?.Content is MemorySortTableViewModel table)
        {
            table.ShowPart(found);
        }
    }

    public void OpenSortTable(ulong iterator, bool isBelowPlan)
    {
        if (FindSortTable(iterator) is not { } document)
        {
            var table = new MemorySortTableViewModel(Query.MemoryHistory, iterator, Playhead ?? TracePosition.Unknown, this);

            table.SetPlayhead(Playhead);

            document = new DocumentViewModel(table.Title,
                                             table,
                                             static () => new MemoryStructureView(),
                                             keepAlive: true,
                                             key: table.Key,
                                             persist: false)
            {
                MinWidth = HexViewControl.StandardWidth
            };

            StructureDocuments.Add(document);
        }

        ShowStructure(document, isBelowPlan);
    }

    public void OpenDataPath()
    {
        if (FindDataPath() is not { } document)
        {
            var dataPath = new MemoryDataPathViewModel(Query.MemoryHistory, Playhead ?? TracePosition.Unknown, this);

            dataPath.SetPlayhead(Playhead);

            document = new DocumentViewModel(dataPath.Title,
                                             dataPath,
                                             static () => new MemoryStructureView(),
                                             keepAlive: true,
                                             key: dataPath.Key,
                                             persist: false)
            {
                MinWidth = HexViewControl.StandardWidth
            };

            Query.Layout.RegisterDocument(document.Key, document);

            StructureDocuments.Add(document);
        }

        Query.Layout.ShowBesideTraceTimeline(document);
    }

    public List<TraceMenuItem> GetMenuItems(TraceMenuTarget target)
    {
        var items = new List<TraceMenuItem>();

        if (Trace is { } trace
            && TraceHashTableReader.GetCallTarget(trace.MemoryHistory,
                                                  trace.CallStore,
                                                  target.Node,
                                                  target.CallIndex) is var (group, location))
        {
            items.Add(new TraceMenuItem("Open Hash Table",
                                        () => OpenHashTable(group, location?.Partition ?? -1, location?.Bucket ?? -1, isBelowPlan: false)));
        }

        foreach (var contributor in Query.MenuContributors)
        {
            contributor.AddItems(items, target, this);
        }

        return items;
    }

    public void OpenDocument(string key, Func<IPluginDocument> create)
    {
        if (PluginDocuments.FirstOrDefault(d => d.Key == key) is not { } document)
        {
            var plugin = create();

            document = new DocumentViewModel(plugin.Title, plugin, plugin.CreateView, keepAlive: true, key: key, persist: false);

            Query.Layout.RegisterDocument(key, document);

            PluginDocuments.Add(document);
        }

        ShowPluginDocument(document);
    }

    public void SetTrim(TracePosition? start, TracePosition? end)
    {
        Query.MemoryHistory.SetScope(start, end);

        foreach (var document in GraphDocuments)
        {
            ((MemoryReferenceGraphViewModel)document.Content).RefreshScope();
        }
    }

    public void CloseMemory()
    {
        foreach (var document in MemoryDocuments.ToList())
        {
            Dock.Close(document);
        }

        foreach (var document in GraphDocuments.ToList())
        {
            Query.Layout.Close(document);
        }

        foreach (var document in StructureDocuments.ToList())
        {
            if (Dock.Contains(document))
            {
                Dock.Close(document);
            }
            else
            {
                Query.Layout.Close(document);
            }
        }
    }

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        if (position is { } value)
        {
            Query.SetTracePlayhead(value);
        }

        foreach (var document in MemoryDocuments)
        {
            ((PlayheadMemoryViewModel)document.Content).SetPlayhead(position);
        }

        foreach (var document in GraphDocuments)
        {
            ((MemoryReferenceGraphViewModel)document.Content).SetPlayhead(position);
        }

        foreach (var document in StructureDocuments)
        {
            ((MemoryStructureViewModel)document.Content).SetPlayhead(position);
        }
    }

    public void ResetLayout()
    {
        var documents = Dock.Groups().SelectMany(g => g.Documents).ToArray();

        Dock.SetRoot(documents.Length == 0 ? new FixedNode() : BelowChart(new TabGroupNode(documents)));
    }

    public void Dispose()
    {
        Dock.DocumentClosed -= OnDocumentClosed;

        Dock.LayoutChanged -= OnDockLayoutChanged;

        Dock.SelectionChanged -= OnDockSelectionChanged;

        Dock.DocumentActivated -= OnDocumentActivated;

        Query.Layout.Dock.DocumentClosed -= OnQueryDocumentClosed;

        CallStackDocument.DisposeView();

        foreach (var document in MemoryDocuments)
        {
            DisposeMemory(document);
        }

        MemoryDocuments.Clear();

        foreach (var document in StructureDocuments)
        {
            Query.Layout.Close(document);

            Query.Layout.RemoveDocument(document.Key, out _);

            DisposeStructure(document);
        }

        StructureDocuments.Clear();

        foreach (var document in GraphDocuments)
        {
            Query.Layout.Close(document);

            Query.Layout.RemoveDocument(document.Key, out _);

            DisposeGraph(document);
        }

        GraphDocuments.Clear();

        foreach (var document in PluginDocuments)
        {
            Query.Layout.Close(document);

            Query.Layout.RemoveDocument(document.Key, out _);

            DisposePluginDocument(document);
        }

        PluginDocuments.Clear();
    }

    partial void OnIsCallStackVisibleChanged(bool value)
    {
        if (value == Dock.Contains(CallStackDocument))
        {
            return;
        }

        if (value)
        {
            ShowPanel(CallStackDocument, MemoryDocuments.LastOrDefault(Dock.Contains));
        }
        else
        {
            Dock.Close(CallStackDocument);
        }

        ShowActiveMemoryUpdates();
    }

    private void ShowPanel(DocumentViewModel document, DocumentViewModel? neighbour)
    {
        if (Dock.Contains(document))
        {
            Dock.Activate(document);

            return;
        }

        if (neighbour is not null && Dock.Contains(neighbour))
        {
            Dock.ShowBeside(document, neighbour);

            return;
        }

        if (Dock.Groups().Any())
        {
            Dock.Show(document);

            return;
        }

        Dock.SetRoot(BelowChart(new TabGroupNode(document)));
    }

    private static SplitNode BelowChart(TabGroupNode group)
        => new(Orientation.Vertical, new FixedNode(), group) { FirstStar = ChartStar, SecondStar = PanelStar };

    private FrameworkElement CreateCallStackCommands()
        => TabCommands.Panel(CommandSpacing,
                             new DecodeToggle(),
                             TabCommands.Button(TabCommands.Glyph(StepBackGlyph),
                                                "Previous Frame",
                                                (_, _) => PlayheadStack.Step(forward: false)),
                             TabCommands.Button(TabCommands.Glyph(StepForwardGlyph),
                                                "Next Frame",
                                                (_, _) => PlayheadStack.Step(forward: true)));

    private static FrameworkElement CreateMemoryCommands(PlayheadMemoryViewModel memory)
        => TabCommands.Panel(CommandSpacing,
                             TabCommands.Button(TabCommands.Glyph(StepBackGlyph),
                                                "Previous Update",
                                                (_, _) => memory.StepWrite(isForward: false)),
                             TabCommands.Button(TabCommands.Glyph(StepForwardGlyph),
                                                "Next Update",
                                                (_, _) => memory.StepWrite(isForward: true)));

    private static FrameworkElement CreateGraphCommands(MemoryReferenceGraphViewModel graph)
    {
        var toggles = new ToggleButton[2];

        toggles[0] = TabCommands.Toggle(TabCommands.Label("Reference Changes"),
                                        graph.IsReferenceHot,
                                        "Show only the pointers that changed after they were set up, and the paths to them",
                                        (_, _) => SetHot(0));

        toggles[1] = TabCommands.Toggle(TabCommands.Label("Data Changes"),
                                        graph.IsDataHot,
                                        "Show only the objects whose other fields changed after they were set up, and the paths to them",
                                        (_, _) => SetHot(1));

        return TabCommands.Panel(CommandSpacing, toggles);

        void SetHot(int index)
        {
            toggles[1 - index].IsChecked = false;

            graph.SetHot(TabCommands.IsChecked(toggles[0]), TabCommands.IsChecked(toggles[1]));
        }
    }

    private void OnDockLayoutChanged(object? sender, EventArgs e)
    {
        IsCallStackVisible = Dock.Contains(CallStackDocument);

        ShowActiveMemoryUpdates();
    }

    private void OnDockSelectionChanged(object? sender, EventArgs e) => ShowActiveMemoryUpdates();

    private void OnDocumentActivated(object? sender, DocumentViewModel? document)
    {
        if (document is not null && MemoryDocuments.Contains(document))
        {
            LastActivatedMemory = document;
        }

        ShowActiveMemoryUpdates();
    }

    private void OnDocumentClosed(object? sender, DocumentViewModel document)
    {
        if (MemoryDocuments.Remove(document))
        {
            DisposeMemory(document);
        }
        else if (StructureDocuments.Remove(document))
        {
            DisposeStructure(document);
        }

        ShowActiveMemoryUpdates();
    }

    private void ShowActiveMemoryUpdates()
    {
        var shown = MemoryDocuments.Where(d => ReferenceEquals(Dock.FindGroup(d)?.SelectedDocument, d)).ToList();

        var active = LastActivatedMemory is { } last && shown.Contains(last) ? last : shown.LastOrDefault();

        if (ReferenceEquals(active, ShownMemory))
        {
            return;
        }

        ShownMemory = active;

        Chart?.ShowMemoryUpdates((active?.Content as PlayheadMemoryViewModel)?.GetUpdates(), active?.Title);
    }

    private void OnMemoryPlayheadRequested(object? sender, TracePoint point) => RequestPlayhead(point);

    private void OnMemoryOpened(object? sender, EventArgs e)
    {
        if (sender is PlayheadMemoryViewModel memory && ShownMemory is { } shown && ReferenceEquals(shown.Content, memory))
        {
            Chart?.ShowMemoryUpdates(memory.GetUpdates(), shown.Title);
        }
    }

    private void OnMemoryRequested(object? sender, MemoryRequest request)
    {
        if (sender is not PlayheadMemoryViewModel memory)
        {
            return;
        }

        var position = request.Position ?? memory.Position;

        var block = Query.MemoryHistory.GetBlock(request.Address, position);

        if (block >= 0)
        {
            OpenMemory(block, request.Address, position, request.Length);
        }
    }

    private void AddGraph(string key, MemoryReferenceGraphViewModel graph)
    {
        graph.SetPlayhead(Playhead);

        var document = new DocumentViewModel(graph.Title,
                                             graph,
                                             static () => new MemoryReferenceGraphView(),
                                             keepAlive: true,
                                             key: key,
                                             persist: false,
                                             commandsFactory: () => CreateGraphCommands(graph));

        Query.Layout.RegisterDocument(key, document);

        ShowGraph(document);

        GraphDocuments.Add(document);
    }

    private void ShowGraph(DocumentViewModel document)
    {
        var dock = Query.Layout.Dock;

        if (GraphDocuments.FirstOrDefault(d => d != document && dock.Contains(d)) is { } neighbour)
        {
            dock.ShowBeside(document, neighbour);
        }
        else
        {
            dock.DockBottom(document);
        }
    }

    private void OnQueryDocumentClosed(object? sender, DocumentViewModel document)
    {
        if (GraphDocuments.Remove(document))
        {
            Query.Layout.RemoveDocument(document.Key, out _);

            DisposeGraph(document);
        }
        else if (PluginDocuments.Remove(document))
        {
            Query.Layout.RemoveDocument(document.Key, out _);

            DisposePluginDocument(document);
        }
        else if (StructureDocuments.Remove(document))
        {
            Query.Layout.RemoveDocument(document.Key, out _);

            DisposeStructure(document);
        }
    }

    private static void DisposePluginDocument(DocumentViewModel document)
    {
        document.DisposeView();

        (document.Content as IDisposable)?.Dispose();
    }

    private void ShowPluginDocument(DocumentViewModel document)
    {
        var dock = Query.Layout.Dock;

        if (dock.Contains(document))
        {
            dock.Show(document);
        }
        else if (PluginDocuments.FirstOrDefault(d => d != document && dock.Contains(d)) is { } neighbour)
        {
            dock.ShowBeside(document, neighbour);
        }
        else
        {
            dock.DockBottom(document);
        }
    }

    private DocumentViewModel? FindHashTable(ulong group)
        => StructureDocuments.FirstOrDefault(d => d.Content is MemoryHashTableViewModel table && table.Contains(group));

    private DocumentViewModel? FindSortTable(ulong iterator)
        => StructureDocuments.FirstOrDefault(d => d.Content is MemorySortTableViewModel table && table.Contains(iterator));

    private DocumentViewModel? FindDataPath() => StructureDocuments.FirstOrDefault(d => d.Content is MemoryDataPathViewModel);

    private void ShowStructure(DocumentViewModel document, bool isBelowPlan)
    {
        if (Query.Layout.Dock.Contains(document))
        {
            Query.Layout.Dock.Activate(document);
        }
        else if (isBelowPlan && !Dock.Contains(document))
        {
            Query.Layout.RegisterDocument(document.Key, document);

            Query.Layout.ShowBelowPlan(document);
        }
        else
        {
            Query.Layout.ShowTraceTimeline();

            ShowPanel(document, MemoryDocuments.LastOrDefault(Dock.Contains) ?? CallStackDocument);
        }
    }

    private static void DisposeStructure(DocumentViewModel document)
    {
        document.DisposeView();

        ((MemoryStructureViewModel)document.Content).Close();
    }

    private static void DisposeGraph(DocumentViewModel document)
    {
        document.DisposeView();

        ((MemoryReferenceGraphViewModel)document.Content).Close();
    }

    private void DisposeMemory(DocumentViewModel document)
    {
        document.DisposeView();

        var memory = (PlayheadMemoryViewModel)document.Content;

        memory.PlayheadRequested -= OnMemoryPlayheadRequested;

        memory.MemoryRequested -= OnMemoryRequested;

        memory.Opened -= OnMemoryOpened;

        memory.Close();
    }
}
