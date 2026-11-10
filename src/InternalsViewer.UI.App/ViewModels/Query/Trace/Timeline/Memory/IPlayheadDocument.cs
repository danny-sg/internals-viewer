using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public interface IPlayheadDocument
{
    string Title { get; }

    void SetPlayhead(TracePosition? position);

    void Close();
}
