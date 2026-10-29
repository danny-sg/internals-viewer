using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryDetailsViewModel : ObservableObject
{
    private const string UnknownFunction = "Unknown";

    private const string EndOfRecording = "End Of Recording";

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private string _length = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InitialisedAtText))]
    [NotifyPropertyChangedFor(nameof(LifetimeText))]
    private TracePoint? _initialisedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DestroyedAtText))]
    [NotifyPropertyChangedFor(nameof(LifetimeText))]
    [NotifyPropertyChangedFor(nameof(IsDestroyed))]
    private TracePoint? _destroyedAt;

    [ObservableProperty]
    private string _initialisedBy = string.Empty;

    [ObservableProperty]
    private string _updateCount = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WritersText))]
    private List<MemoryWriterSummary> _writers = [];

    public string InitialisedAtText => InitialisedAt is { } point ? TracePositionText.GetHexText(point.Position) : string.Empty;

    public string DestroyedAtText => DestroyedAt is { } point ? TracePositionText.GetHexText(point.Position) : EndOfRecording;

    public string LifetimeText => InitialisedAt is null ? string.Empty : $"{InitialisedAtText} - {DestroyedAtText}";

    public string WritersText => string.Join(Environment.NewLine, Writers.Select(w => $"{w.Label} {w.Count}"));

    public bool IsDestroyed => DestroyedAt is not null;

    public ulong BlockAddress { get; private set; }

    public TraceCallLink? InitialisedLink { get; private set; }

    public TraceCallLink? DestroyedLink { get; private set; }

    public void Load(PlayheadMemoryViewModel owner)
    {
        var store = owner.History.Store;

        var block = store.Blocks[owner.Block];

        var allocatingCall = owner.History.GetAllocatingCall(owner.Block);

        BlockAddress = block.Address;

        InitialisedLink = owner.History.GetCallLink(block.Thread, block.From, allocatingCall);

        DestroyedLink = block.To is { } to ? owner.History.GetCallLink(block.Thread, to, null) : null;

        Address = $"0x{block.Address:X16}";

        Type = owner.History.GetObjectName(owner.Block);

        Length = $"{block.Size:N0} Bytes";

        InitialisedAt = block.From;

        DestroyedAt = block.To;

        InitialisedBy = allocatingCall?.Symbol ?? UnknownFunction;

        var writes = store.GetWrites(owner.Block);

        UpdateCount = $"{writes.Count:N0}";

        Writers = [.. Enumerable.Range(0, writes.Count)
                                .GroupBy(i => writes[i].CallNodeIndex)
                                .Select(g => GetWriter(owner, g.Key, g.First(), g.Count()))];
    }

    public void Clear()
    {
        Address = string.Empty;

        Type = string.Empty;

        Length = string.Empty;

        BlockAddress = 0;

        InitialisedLink = null;

        DestroyedLink = null;

        InitialisedAt = null;

        DestroyedAt = null;

        InitialisedBy = string.Empty;

        UpdateCount = string.Empty;

        Writers = [];
    }

    private static MemoryWriterSummary GetWriter(PlayheadMemoryViewModel owner, int callNodeIndex, int first, int count)
    {
        var store = owner.History.Store;

        var block = store.Blocks[owner.Block];

        var write = store.GetWrites(owner.Block)[first];

        var link = new TraceCallLink(store.GetPoint(block.FirstWrite + first), owner.History.GetWriter(callNodeIndex), write.CallIndex);

        return new MemoryWriterSummary(owner.History.GetWriterLabel(callNodeIndex), owner.GetCategoryColour(callNodeIndex), count, link);
    }
}
