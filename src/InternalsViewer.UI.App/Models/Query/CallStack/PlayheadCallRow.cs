using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed partial class PlayheadCallRow(CallStackNode node,
                                            TraceTimelineSpan span,
                                            CallSignature signature,
                                            ulong id,
                                            bool isCurrent,
                                            TraceCallExtent extent = default,
                                            double positionOrigin = 0) : ObservableObject
{
    private const string ThisLabel = "this";

    [ObservableProperty]
    private CallSignaturePart? _highlighted;

    public CallStackNode Node { get; } = node;

    public ulong Id { get; } = id;

    public int Call => span.Call;

    public double Start => span.Start;

    public double End => span.End;

    public IReadOnlyList<CallSignaturePart> Signature => signature.Parts;

    public CallSignaturePart? This => signature.This;

    public CallSignaturePart? Return => signature.Return;

    public bool IsCurrent { get; } = isCurrent;

    public long PositionStart => (long)Math.Floor(extent.PositionStart - positionOrigin);

    public long PositionEnd => (long)Math.Floor(extent.PositionEnd - positionOrigin);

    public long Instructions => (long)extent.Instructions;

    public IReadOnlyList<CallSignaturePart> Arguments
        => field ??= [.. Signature.Where(p => p.Kind == CallSignaturePartKind.Value && p.Label != ThisLabel)];
}
