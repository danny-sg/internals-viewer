using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Controls.Query.Trace.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

public sealed partial class TraceTimelineTabViewModel : ObservableObject, IDisposable
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

    public void ShowCallStack() => IsCallStackVisible = true;

    public void RequestPlayhead(TracePoint point) => PlayheadRequested?.Invoke(this, point);

    public void OpenMemory(int block, ulong address, TracePosition position, int length = 1)
    {
        Query.Layout.ShowTraceTimeline();

        var key = $"Memory{block}";

        if (MemoryDocuments.FirstOrDefault(d => d.Key == key) is { } existing)
        {
            Dock.Activate(existing);

            (existing.Content as PlayheadMemoryViewModel)?.ShowAddress(address, length);

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
            MinWidth = HexViewControl.StandardWidth
        };

        var neighbour = MemoryDocuments.LastOrDefault(Dock.Contains) ?? CallStackDocument;

        MemoryDocuments.Add(document);

        ShowPanel(document, neighbour);
    }

    public void CloseMemory()
    {
        foreach (var document in MemoryDocuments.ToList())
        {
            Dock.Close(document);
        }
    }

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        foreach (var document in MemoryDocuments)
        {
            ((PlayheadMemoryViewModel)document.Content).SetPlayhead(position);
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

        CallStackDocument.DisposeView();

        foreach (var document in MemoryDocuments)
        {
            DisposeMemory(document);
        }

        MemoryDocuments.Clear();
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

    private void OnDockLayoutChanged(object? sender, EventArgs e) => IsCallStackVisible = Dock.Contains(CallStackDocument);

    private void OnDocumentClosed(object? sender, DocumentViewModel document)
    {
        if (MemoryDocuments.Remove(document))
        {
            DisposeMemory(document);
        }
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

    private void DisposeMemory(DocumentViewModel document)
    {
        document.DisposeView();

        var memory = (PlayheadMemoryViewModel)document.Content;

        memory.PlayheadRequested -= OnMemoryPlayheadRequested;

        memory.MemoryRequested -= OnMemoryRequested;

        memory.Close();
    }
}
