using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Stack;

public sealed partial class PlayheadCallRow(CallTreeNode node,
                                            TraceSpan span,
                                            CallSignature signature,
                                            TracePosition fromPosition,
                                            TracePosition toPosition,
                                            bool isCurrent,
                                            ulong originSequence = 0,
                                            string? objectClass = null) : ObservableObject
{
    [ObservableProperty]
    private CallSignaturePart? _highlighted;

    public CallTreeNode Node { get; } = node;

    public TraceSpan Span { get; } = span;

    public TracePosition FromPosition { get; } = fromPosition;

    public TracePosition ToPosition { get; } = toPosition;

    public ulong OriginSequence { get; } = originSequence;

    public int CallIndex => Span.CallIndex;

    public IReadOnlyList<CallSignaturePart> Signature => signature.Parts;

    public CallSignaturePart? This => signature.This;

    public CallSignaturePart? Return => signature.Return;

    public bool IsCurrent { get; } = isCurrent;

    public string? ObjectClass { get; } = objectClass;

    public string? BaseClass
        => ObjectClass is not null && Node.Frame?.Function?.ClassName is { Length: > 0 } functionClass && functionClass != ObjectClass
            ? functionClass
            : null;

    public string CurrentText { get; set; } = string.Empty;

    public long Instructions => (long)Span.Instructions;

    public IReadOnlyList<CallSignaturePart> Arguments
        => field ??= [.. Signature.Where(p => p.Kind == CallSignaturePartKind.Value && !p.IsThis)];
}
