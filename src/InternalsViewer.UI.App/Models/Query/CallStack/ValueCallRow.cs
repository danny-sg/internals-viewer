using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueCallRow(TraceValueMatch match, TraceValueUse use, IReadOnlyList<CallSignaturePart> signature)
{
    public TraceValueMatch Match { get; } = match;

    public TraceValueUse Use { get; } = use;

    public IReadOnlyList<CallSignaturePart> Signature { get; } = signature;

    public string Call => $"Call {Match.Call + 1:N0}";

    public string Thread => $"Thread {Match.Thread}";
}
