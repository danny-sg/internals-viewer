using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Menus;
using InternalsViewer.Query.FullTrace.InstructionHistory.Settings;
using InternalsViewer.UI.Plugins.Documents;
using InternalsViewer.UI.Plugins.FullTrace;
using InternalsViewer.UI.Plugins.FullTrace.Menus;
using InternalsViewer.UI.Plugins.FullTrace.Menus.Enums;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests.Menus;

[Trait("Category", "Unit")]
public class InstructionHistoryMenuContributorTests
{
    private static readonly CallStackNode Node = new() { Frame = new CallstackFrame { Address = 0x7FF8_0060_0000 } };

    [Fact]
    public void A_Target_Without_A_Call_Adds_Nothing()
    {
        var items = AddItems(new TraceMenuTarget(TraceMenuSite.Timeline, Node, -1));

        Assert.Empty(items);
    }

    [Fact]
    public void A_Target_Without_A_Node_Adds_Nothing()
    {
        var items = AddItems(new TraceMenuTarget(TraceMenuSite.Timeline, null, 0));

        Assert.Empty(items);
    }

    [Fact]
    public void A_Run_Without_A_Full_Trace_Adds_Nothing()
    {
        var items = AddItems(new TraceMenuTarget(TraceMenuSite.Timeline, Node, 0));

        Assert.Empty(items);
    }

    private static List<TraceMenuItem> AddItems(TraceMenuTarget target)
    {
        var contributor = new InstructionHistoryMenuContributor(new InstructionListingBuilder(),
                                                                new InstructionHistorySettings(new FakePluginSettingStore()));

        var items = new List<TraceMenuItem>();

        contributor.AddItems(items, target, new FakeTracePluginHost());

        return items;
    }

    private sealed class FakeTracePluginHost : ITracePluginHost
    {
        public TraceResult? Trace => null;

        public void OpenDocument(string key, Func<IPluginDocument> create)
        {
        }
    }
}
