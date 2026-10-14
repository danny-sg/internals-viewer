using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueNoteRow(string label, IReadOnlyList<TraceValueCall>? valueCalls = null, int from = 0)
{
    public static ValueNoteRow Loading { get; } = new("Loading...");

    public string Label { get; } = label;

    public IReadOnlyList<TraceValueCall>? ValueCalls { get; } = valueCalls;

    public int From { get; } = from;

    public static ValueNoteRow More(IReadOnlyList<TraceValueCall> valueCalls, int from)
        => new($"Show More ({valueCalls.Count - from:N0} Remaining)", valueCalls, from);
}
