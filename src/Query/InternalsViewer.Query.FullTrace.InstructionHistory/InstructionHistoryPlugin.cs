using InternalsViewer.Query.FullTrace.InstructionHistory;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Menus;
using InternalsViewer.Query.FullTrace.InstructionHistory.Settings;
using InternalsViewer.UI.Plugins;
using InternalsViewer.UI.Plugins.Enums;
using InternalsViewer.UI.Plugins.FullTrace.Menus;
using InternalsViewer.UI.Plugins.FullTrace.Settings;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsViewerPlugin(typeof(InstructionHistoryPlugin))]

namespace InternalsViewer.Query.FullTrace.InstructionHistory;

public sealed class InstructionHistoryPlugin : IInternalsViewerPlugin
{
    public PluginArea Area => PluginArea.FullTrace;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IInstructionListingBuilder, InstructionListingBuilder>();

        services.AddSingleton<InstructionHistorySettings>();

        services.AddSingleton<ITraceMenuContributor, InstructionHistoryMenuContributor>();

        services.AddSingleton<ITraceSettingsContributor, InstructionHistorySettingsContributor>();
    }
}
