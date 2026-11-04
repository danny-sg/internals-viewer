namespace InternalsViewer.UI.Plugins;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class InternalsViewerPluginAttribute(Type pluginType) : Attribute
{
    public Type PluginType { get; } = pluginType;
}
