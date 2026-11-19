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

    private const string StackMemory = "Stack";

    private const string HeapMemory = "Heap";

    private const string ListStart = "[";

    private const string ListSeparator = ", ";

    private const string ListEnd = "]";

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMemoryKind))]
    private string _memoryKind = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadVisible))]
    private bool _isStack;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTypeParts))]
    [NotifyPropertyChangedFor(nameof(IsTypeText))]
    private List<MemoryTypePart> _typeParts = [];

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
    [NotifyPropertyChangedFor(nameof(HasInstances))]
    private string _instancesText = string.Empty;

    [ObservableProperty]
    private string _initialisedBy = string.Empty;

    [ObservableProperty]
    private string _updateCount = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WritersText))]
    private List<MemoryWriterSummary> _writers = [];

    [ObservableProperty]
    private string _readCount = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadersText))]
    private List<MemoryWriterSummary> _readers = [];

    public string InitialisedAtText => InitialisedAt is { } point ? TracePositionText.GetHexText(point.Position) : string.Empty;

    public string DestroyedAtText => DestroyedAt is { } point ? TracePositionText.GetHexText(point.Position) : EndOfRecording;

    public string LifetimeText => InitialisedAt is null ? string.Empty : $"{InitialisedAtText} - {DestroyedAtText}";

    public string WritersText => string.Join(Environment.NewLine, Writers.Select(w => $"{w.Label} {w.Count}"));

    public string ReadersText => string.Join(Environment.NewLine, Readers.Select(r => $"{r.Label} {r.Count}"));

    public bool IsDestroyed => DestroyedAt is not null;

    public bool HasInstances => InstancesText.Length > 0;

    public bool HasMemoryKind => MemoryKind.Length > 0;

    public bool HasTypeParts => TypeParts.Count > 0;

    public bool IsTypeText => TypeParts.Count == 0;

    public bool IsReadVisible => !IsStack;

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

        IsStack = block.IsStack;

        MemoryKind = block.IsStack ? StackMemory : HeapMemory;

        Type = owner.History.GetObjectName(owner.Block);

        TypeParts = GetTypeParts(owner, block.Address, block.Size);

        Length = $"{block.Size:N0} Bytes";

        InitialisedAt = block.From;

        DestroyedAt = block.To;

        InstancesText = block.InstanceCount switch
        {
            0 => string.Empty,
            1 => "1 Instance",
            var count => $"{count:N0} Instances"
        };

        InitialisedBy = allocatingCall?.Symbol ?? UnknownFunction;

        var writes = store.GetWrites(owner.Block);

        UpdateCount = $"{writes.Count:N0}";

        Writers = [.. Enumerable.Range(0, writes.Count)
                                .GroupBy(i => writes[i].ReplayNodeIndex)
                                .Select(g => GetWriter(owner, g.Key, g.First(), g.Count()))];

        var reads = store.GetReads(owner.Block);

        ReadCount = $"{reads.Sum(r => (long)r.Count):N0}";

        Readers = [.. Enumerable.Range(0, reads.Count)
                                .GroupBy(i => reads[i].ReplayNodeIndex)
                                .Select(g => GetReader(owner, g.Key, g.First(), g.Sum(i => reads[i].Count)))];
    }

    public void Clear()
    {
        Address = string.Empty;

        IsStack = false;

        MemoryKind = string.Empty;

        Type = string.Empty;

        TypeParts = [];

        Length = string.Empty;

        BlockAddress = 0;

        InitialisedLink = null;

        DestroyedLink = null;

        InitialisedAt = null;

        DestroyedAt = null;

        InstancesText = string.Empty;

        InitialisedBy = string.Empty;

        UpdateCount = string.Empty;

        Writers = [];

        ReadCount = string.Empty;

        Readers = [];
    }

    private static List<MemoryTypePart> GetTypeParts(PlayheadMemoryViewModel owner, ulong address, int size)
    {
        var pointers = owner.History.GetListedPointers(owner.Block);

        if (pointers.Count == 0)
        {
            return [];
        }

        List<MemoryTypePart> parts = [new(PointerArrayText.PointerName, () => owner.RequestMemory(address, size)), new(ListStart, null)];

        for (var index = 0; index < Math.Min(pointers.Count, PointerArrayText.MaximumShown); index++)
        {
            if (index > 0)
            {
                parts.Add(new(ListSeparator, null));
            }

            var (name, pointer) = pointers[index];

            parts.Add(new(name, () => owner.OpenPointer(pointer)));
        }

        if (pointers.Count > PointerArrayText.MaximumShown)
        {
            parts.Add(new($"{ListSeparator}{PointerArrayText.GetMoreText(pointers.Count)}", null));
        }

        parts.Add(new(ListEnd, null));

        return parts;
    }

    private static MemoryWriterSummary GetWriter(PlayheadMemoryViewModel owner, int replayNodeIndex, int first, int count)
    {
        var store = owner.History.Store;

        var block = store.Blocks[owner.Block];

        var write = store.GetWrites(owner.Block)[first];

        var link = new TraceCallLink(store.GetPoint(block.FirstWrite + first), owner.History.GetWriter(replayNodeIndex), write.CallIndex);

        return new MemoryWriterSummary(owner.History.GetWriterLabel(replayNodeIndex),
                                       owner.GetCategoryColour(replayNodeIndex),
                                       count,
                                       link);
    }

    private static MemoryWriterSummary GetReader(PlayheadMemoryViewModel owner, int replayNodeIndex, int first, int count)
    {
        var store = owner.History.Store;

        var (start, _) = store.GetReadRange(owner.Block);

        var read = store.GetRead(start + first);

        var link = new TraceCallLink(store.GetReadPoint(start + first), owner.History.GetWriter(replayNodeIndex), read.CallIndex);

        return new MemoryWriterSummary(owner.History.GetWriterLabel(replayNodeIndex), owner.GetReadColour(replayNodeIndex), count, link);
    }
}
