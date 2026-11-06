using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures;

public sealed record CallSignature(IReadOnlyList<CallSignaturePart> Parts, CallSignaturePart? This, CallSignaturePart? Return);
