using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueCallRow(TraceValueCall valueCall, TraceValueUse use, IReadOnlyList<CallSignaturePart> signature)
{
    public TraceValueCall ValueCall { get; } = valueCall;

    public TraceValueUse Use { get; } = use;

    public IReadOnlyList<CallSignaturePart> Signature { get; } = signature;

    public string Call => $"Call {ValueCall.CallIndex + 1}";

    public string Thread => $"Thread {ValueCall.Thread}";
}
