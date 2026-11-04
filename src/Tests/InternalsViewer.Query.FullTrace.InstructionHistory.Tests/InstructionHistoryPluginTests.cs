using InternalsViewer.Query.FullTrace.InstructionHistory.Menus;
using InternalsViewer.Query.FullTrace.InstructionHistory.Settings;
using InternalsViewer.UI.Plugins.Enums;
using InternalsViewer.UI.Plugins.FullTrace.Menus;
using InternalsViewer.UI.Plugins.FullTrace.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests;

[Trait("Category", "Unit")]
public class InstructionHistoryPluginTests
{
    [Fact]
    public void The_Plugin_Targets_Full_Trace() => Assert.Equal(PluginArea.FullTrace, new InstructionHistoryPlugin().Area);

    [Fact]
    public void The_Plugin_Adds_A_Call_Menu_And_A_Setting()
    {
        var services = new ServiceCollection();

        new InstructionHistoryPlugin().ConfigureServices(services);

        Assert.Contains(services, s => s.ServiceType == typeof(ITraceMenuContributor)
                                       && s.ImplementationType == typeof(InstructionHistoryMenuContributor));

        Assert.Contains(services, s => s.ServiceType == typeof(ITraceSettingsContributor)
                                       && s.ImplementationType == typeof(InstructionHistorySettingsContributor));
    }
}
