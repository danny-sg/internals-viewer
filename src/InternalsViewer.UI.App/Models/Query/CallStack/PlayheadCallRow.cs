using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed partial class PlayheadCallRow(CallStackNode node,
                                            TraceStackCall stackCall,
                                            CallSignature signature,
                                            ulong id,
                                            bool isCurrent,
                                            double positionOrigin = 0) : ObservableObject
{
    [ObservableProperty]
    private CallSignaturePart? _highlighted;

    public CallStackNode Node { get; } = node;

    public ulong Id { get; } = id;

    public int Call => stackCall.Span.Call;

    public double Start => stackCall.Span.Start;

    public double End => stackCall.Span.End;

    public IReadOnlyList<CallSignaturePart> Signature => signature.Parts;

    public CallSignaturePart? This => signature.This;

    public CallSignaturePart? Return => signature.Return;

    public bool IsCurrent { get; } = isCurrent;

    public long PositionStart => (long)Math.Floor(stackCall.PositionStart - positionOrigin);

    public long PositionEnd => (long)Math.Floor(stackCall.PositionEnd - positionOrigin);

    public long Instructions => (long)stackCall.Instructions;

    public IReadOnlyList<CallSignaturePart> Arguments
        => field ??= [.. Signature.Where(p => p.Kind == CallSignaturePartKind.Value && !p.IsThis)];
}
