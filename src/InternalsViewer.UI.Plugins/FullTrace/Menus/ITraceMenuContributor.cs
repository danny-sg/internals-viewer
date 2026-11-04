namespace InternalsViewer.UI.Plugins.FullTrace.Menus;

public interface ITraceMenuContributor
{
    void AddItems(List<TraceMenuItem> items, TraceMenuTarget target, ITracePluginHost host);
}
