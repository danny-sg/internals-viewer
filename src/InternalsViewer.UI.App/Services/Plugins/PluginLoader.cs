using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InternalsViewer.UI.Plugins;
using InternalsViewer.UI.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.Services.Plugins;

public static class PluginLoader
{
    public const string MetadataKey = "InternalsViewerPlugin";

    private static readonly PluginArea[] SupportedAreas = [PluginArea.FullTrace];

    public static void RegisterPlugins(this IServiceCollection services, Assembly host)
    {
        foreach (var plugin in GetPlugins(host))
        {
            plugin.ConfigureServices(services);
        }
    }

    public static List<IInternalsViewerPlugin> GetPlugins(Assembly host)
    {
        var plugins = new List<IInternalsViewerPlugin>();

        var names = host.GetCustomAttributes<AssemblyMetadataAttribute>()
                        .Where(a => a.Key == MetadataKey && !string.IsNullOrEmpty(a.Value))
                        .Select(a => a.Value!);

        foreach (var name in names)
        {
            var assembly = Assembly.Load(new AssemblyName(name));

            foreach (var attribute in assembly.GetCustomAttributes<InternalsViewerPluginAttribute>())
            {
                if (Activator.CreateInstance(attribute.PluginType) is IInternalsViewerPlugin plugin
                    && SupportedAreas.Contains(plugin.Area))
                {
                    plugins.Add(plugin);
                }
            }
        }

        return plugins;
    }
}
