using System.Collections.Generic;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Stack;

public sealed class PlayheadThreadRow(uint threadId, IReadOnlyList<PlayheadCallRow> calls)
{
    public uint ThreadId { get; } = threadId;

    public IReadOnlyList<PlayheadCallRow> Calls { get; } = calls;

    public string Label => $"Thread {ThreadId}";

    public string Detail => CountText.GetCalls(Calls.Count);
}
