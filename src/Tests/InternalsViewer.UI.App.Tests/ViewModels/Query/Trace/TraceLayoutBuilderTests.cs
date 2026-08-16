using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Query.Trace;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query.Trace;

public class TraceLayoutBuilderTests
{
    [Fact]
    public void A_Hash_Match_Gets_A_Hash_Table_Pane()
    {
        var layout = Build(HashMatch());

        var tab = layout.Tabs.Single(t => t.NodeId == 2);

        Assert.True(tab.IsJoinLayout);
        Assert.Equal(TracePaneKind.HashTable, tab.OuterBottom.Kind);

        Assert.NotNull(layout.Nodes[2].HashTable);
    }

    private static TraceLayout Build(IteratorDefinition definition)
        => TraceLayoutBuilder.Build(definition, new Dictionary<int, TraceVisualViewModel>(), _ => null);

    private static HashMatchDefinition HashMatch()
        => new(new JoinInputDefinition(Range(0), ["Id"]), new JoinInputDefinition(Range(1), ["Id"]))
        {
            NodeId = 2,
            JoinType = JoinType.Inner
        };

    private static RangeDefinition Range(int nodeId)
        => new(1, new PageAddress(1, 100 + nodeId), [SeekBounds.All]) { NodeId = nodeId };
}
