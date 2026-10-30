using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.UI.App.Controls.Query.Trace.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public sealed partial class TraceTimelineTabViewModel : ObservableObject, ITraceNavigator, IDisposable
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

    private QueryViewModel Query { get; }

    private DocumentViewModel CallStackDocument { get; }

    private List<DocumentViewModel> MemoryDocuments { get; } = [];

    private List<DocumentViewModel> GraphDocuments { get; } = [];

    private DocumentViewModel? LastActivatedMemory { get; set; }

    private DocumentViewModel? ShownMemory { get; set; }

    public void ShowCallStack() => IsCallStackVisible = true;

    public void RequestPlayhead(TracePoint point) => PlayheadRequested?.Invoke(this, point);

    public void MovePlayhead(TracePoint point) => RequestPlayhead(point);

    public void ZoomToCall(CallStackNode node, int callIndex) => PlayheadStack.RequestChart(c => c.ZoomToCall(node, callIndex));

    public void ShowCallStack(TracePoint point)
    {
        RequestPlayhead(point);

        ShowCallStack();
    }

    public void FindInCallTree(CallStackNode node, int callIndex) => Query.OpenCallTree(node, callIndex);

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

        memory.Open(block, address, position);

        if (!memory.IsOpen)
        {
            return;
        }

        if (length > 1)
        {
            memory.ShowAddress(address, length);
        }

        memory.SetPlayhead(Playhead);

        memory.Chart = Chart;

        memory.Navigator = this;

        memory.PlayheadRequested += OnMemoryPlayheadRequested;

        memory.MemoryRequested += OnMemoryRequested;

        var document = new DocumentViewModel(memory.Title,
                                             memory,
                                             static () => new TraceTimelineMemoryView(),
                                             keepAlive: true,
                                             key: key,
                                             persist: false,
                                             commandsFactory: () => CreateMemoryCommands(memory))
        {
            MinWidth = HexViewControl.StandardWidth,
            MenuItems = [new DocumentMenuItem("Open Reference Graph", () => OpenReferenceGraph(memory.Block, memory.Position))]
        };

        var neighbour = MemoryDocuments.LastOrDefault(Dock.Contains) ?? CallStackDocument;

        MemoryDocuments.Add(document);

        ShowPanel(document, neighbour);

        LastActivatedMemory = document;

        ShowActiveMemoryUpdates();
    }

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

        var graph = new MemoryReferenceGraphViewModel(Query.MemoryHistory, block, position, this);

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
    }

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        foreach (var document in MemoryDocuments)
        {
            ((PlayheadMemoryViewModel)document.Content).SetPlayhead(position);
        }

        foreach (var document in GraphDocuments)
        {
            ((MemoryReferenceGraphViewModel)document.Content).SetPlayhead(position);
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

        foreach (var document in GraphDocuments)
        {
            Query.Layout.Close(document);

            Query.Layout.RemoveDocument(document.Key, out _);

            DisposeGraph(document);
        }

        GraphDocuments.Clear();
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

    private void OnMemoryPlayheadRequested(object? sender, TracePoint point) => PlayheadRequested?.Invoke(this, point);

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

        memory.Close();
    }
}
