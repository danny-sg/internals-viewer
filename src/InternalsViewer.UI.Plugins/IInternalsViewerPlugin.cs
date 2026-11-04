using InternalsViewer.UI.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.Plugins;

public interface IInternalsViewerPlugin
{
    PluginArea Area { get; }

    void ConfigureServices(IServiceCollection services);
}
