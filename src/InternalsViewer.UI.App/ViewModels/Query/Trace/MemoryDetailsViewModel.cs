using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

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
    private TracePoint? _initialisedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DestroyedAtText))]
    [NotifyPropertyChangedFor(nameof(IsDestroyed))]
    private TracePoint? _destroyedAt;

    [ObservableProperty]
    private string _initialisedBy = string.Empty;

    [ObservableProperty]
    private List<MemoryWriterSummary> _writers = [];

    public string InitialisedAtText => InitialisedAt is { } point ? TracePositionText.GetHexText(point.Position) : string.Empty;

    public string DestroyedAtText => DestroyedAt is { } point ? TracePositionText.GetHexText(point.Position) : EndOfRecording;

    public bool IsDestroyed => DestroyedAt is not null;

    public void Load(PlayheadMemoryViewModel owner)
    {
        var store = owner.History.Store;

        var block = store.Blocks[owner.Block];

        Address = $"0x{block.Address:X16}";

        Type = owner.History.GetObjectName(owner.Block);

        Length = $"{block.Size:N0} Bytes";

        InitialisedAt = block.From;

        DestroyedAt = block.To;

        InitialisedBy = owner.History.GetAllocatingCall(owner.Block)?.Symbol ?? UnknownFunction;

        Writers = [.. store.GetWrites(owner.Block)
                          .GroupBy(w => w.CallNodeIndex)
                          .Select(g => new MemoryWriterSummary(owner.History.GetWriterLabel(g.Key), owner.GetCategoryColour(g.Key), g.Count()))
                          .OrderByDescending(w => w.Count)
                          .ThenBy(w => w.Label)];
    }

    public void Clear()
    {
        Address = string.Empty;

        Type = string.Empty;

        Length = string.Empty;

        InitialisedAt = null;

        DestroyedAt = null;

        InitialisedBy = string.Empty;

        Writers = [];
    }
}
