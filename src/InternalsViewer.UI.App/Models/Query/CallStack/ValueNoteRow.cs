using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueNoteRow(string label, IReadOnlyList<TraceValueMatch>? matches = null, int from = 0)
{
    public static ValueNoteRow Loading { get; } = new("Loading...");

    public string Label { get; } = label;

    public IReadOnlyList<TraceValueMatch>? Matches { get; } = matches;

    public int From { get; } = from;

    public static ValueNoteRow More(IReadOnlyList<TraceValueMatch> matches, int from)
        => new($"Show More ({matches.Count - from:N0} Remaining)", matches, from);
}
