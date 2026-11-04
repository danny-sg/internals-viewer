using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Recording;
using InternalsViewer.UI.Plugins.Documents;

namespace InternalsViewer.UI.Plugins.FullTrace;

public interface ITracePluginHost
{
    TraceResult? Trace { get; }

    TimeTravelTraceFile? TraceFile { get; }

    void OpenDocument(string key, Func<IPluginDocument> create);
}
