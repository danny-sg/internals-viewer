using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Stack;

public sealed class PlayheadThreadRow(uint threadId, IReadOnlyList<PlayheadCallRow> calls)
{
    public uint ThreadId { get; } = threadId;

    public IReadOnlyList<PlayheadCallRow> Calls { get; } = calls;

    public string Label => $"Thread {ThreadId}";

    public string Detail => Calls.Count == 1 ? "1 Call" : $"{Calls.Count:N0} Calls";
}
