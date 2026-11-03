using InternalsViewer.Query.Helpers;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class CallSearchSuggestion(string name, int calls)
{
    public string Name { get; } = name;

    public int Calls { get; } = calls;

    public string CallsText => CountText.GetCalls(Calls);
}
