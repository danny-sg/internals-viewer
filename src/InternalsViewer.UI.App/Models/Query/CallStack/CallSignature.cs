using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed record CallSignature(IReadOnlyList<CallSignaturePart> Parts, CallSignaturePart? This, CallSignaturePart? Return);
