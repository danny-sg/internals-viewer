using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Stack;

public sealed partial class PlayheadCallRow(CallStackNode node,
                                            TraceSpan span,
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

    public TraceSpan Span { get; } = span;

    public TracePosition FromPosition { get; } = fromPosition;

    public TracePosition ToPosition { get; } = toPosition;

    public ulong OriginSequence { get; } = originSequence;

    public uint Thread { get; } = thread;

    public int CallIndex => Span.CallIndex;

    public IReadOnlyList<CallSignaturePart> Signature => signature.Parts;

    public CallSignaturePart? This => signature.This;

    public CallSignaturePart? Return => signature.Return;

    public bool IsCurrent { get; } = isCurrent;

    public string CurrentText { get; set; } = string.Empty;

    public long Instructions => (long)Span.Instructions;

    public IReadOnlyList<CallSignaturePart> Arguments
        => field ??= [.. Signature.Where(p => p.Kind == CallSignaturePartKind.Value && !p.IsThis)];
}
