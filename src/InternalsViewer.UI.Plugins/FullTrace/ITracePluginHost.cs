using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.Plugins.Documents;

namespace InternalsViewer.UI.Plugins.FullTrace;

public interface ITracePluginHost
{
    TraceResult? Trace { get; }

    void OpenDocument(string key, Func<IPluginDocument> create);
}
