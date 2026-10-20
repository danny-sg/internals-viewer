using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed partial class PlayheadCallRow(CallStackNode node,
                                            TraceStackCall stackCall,
                                            CallSignature signature,
                                            TracePosition fromPosition,
                                            TracePosition toPosition,
                                            uint thread,
                                            bool isCurrent,
                                            ulong originSequence = 0) : ObservableObject
{
    [ObservableProperty]
    private CallSignaturePart? _highlighted;

    public CallStackNode Node { get; } = node;

    public TracePosition FromPosition { get; } = fromPosition;

    public TracePosition ToPosition { get; } = toPosition;

    public ulong OriginSequence { get; } = originSequence;

    public uint Thread { get; } = thread;

    public int Call => stackCall.Span.Call;

    public double Start => stackCall.Span.Start;

    public double End => stackCall.Span.End;

    public IReadOnlyList<CallSignaturePart> Signature => signature.Parts;

    public CallSignaturePart? This => signature.This;

    public CallSignaturePart? Return => signature.Return;

    public bool IsCurrent { get; } = isCurrent;

    public string CurrentText { get; set; } = string.Empty;

    public long Instructions => (long)stackCall.Instructions;

    public IReadOnlyList<CallSignaturePart> Arguments
        => field ??= [.. Signature.Where(p => p.Kind == CallSignaturePartKind.Value && !p.IsThis)];
}
