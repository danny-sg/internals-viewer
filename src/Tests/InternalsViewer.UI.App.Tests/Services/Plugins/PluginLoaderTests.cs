using System.Reflection;
using InternalsViewer.UI.App.Services.Plugins;
using InternalsViewer.UI.App.Tests.Services.Plugins;
using InternalsViewer.UI.Plugins;
using InternalsViewer.UI.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyMetadata(PluginLoader.MetadataKey, "InternalsViewer.UI.App.Tests")]
[assembly: InternalsViewerPlugin(typeof(PluginLoaderTests.FullTracePlugin))]
[assembly: InternalsViewerPlugin(typeof(PluginLoaderTests.UnsupportedPlugin))]

namespace InternalsViewer.UI.App.Tests.Services.Plugins;

[Trait("Category", "Unit")]
public class PluginLoaderTests
{
    [Fact]
    public void A_Plugin_Named_By_The_Host_Registers_Its_Services()
    {
        var services = new ServiceCollection();

        services.RegisterPlugins(typeof(PluginLoaderTests).Assembly);

        Assert.Contains(services, s => s.ServiceType == typeof(FullTracePlugin.Marker));
    }

    [Fact]
    public void A_Plugin_For_An_Area_The_App_Does_Not_Support_Is_Skipped()
    {
        var plugins = PluginLoader.GetPlugins(typeof(PluginLoaderTests).Assembly);

        Assert.Single(plugins);
        Assert.IsType<FullTracePlugin>(plugins[0]);
    }

    [Fact]
    public void A_Host_That_Names_No_Plugins_Loads_None() => Assert.Empty(PluginLoader.GetPlugins(typeof(object).Assembly));

    [Fact]
    public void Every_Plugin_The_App_Names_Loads()
    {
        var host = typeof(App).Assembly;

        var named = host.GetCustomAttributes<AssemblyMetadataAttribute>().Count(a => a.Key == PluginLoader.MetadataKey);

        Assert.Equal(named, PluginLoader.GetPlugins(host).Count);
    }

    public sealed class FullTracePlugin : IInternalsViewerPlugin
    {
        public PluginArea Area => PluginArea.FullTrace;

        public void ConfigureServices(IServiceCollection services) => services.AddSingleton<Marker>();

        public sealed class Marker;
    }

    public sealed class UnsupportedPlugin : IInternalsViewerPlugin
    {
        public PluginArea Area => (PluginArea)int.MaxValue;

        public void ConfigureServices(IServiceCollection services)
        {
        }
    }
}
