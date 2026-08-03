using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Parsers;
using InternalsViewer.UI.App.Services.Trace;

namespace InternalsViewer.UI.App.Tests.Services.Trace;

/// <summary>
/// Covers the step from a parsed plan to the definition tree that runs it
/// </summary>
public class TraceDefinitionBuilderTests
{
    /// <summary>
    /// A hash match of two seeks, each bounded only at the top by Id &lt; 100
    /// </summary>
    private const string HashMatchOfTwoSeeksXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.599" Build="17.0.4065.4">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementType="SELECT" StatementSubTreeCost="0.024936">
                  <QueryPlan>
                    <RelOp NodeId="0" PhysicalOp="Hash Match" LogicalOp="Inner Join"
                           EstimatedTotalSubtreeCost="0.024936" EstimateRows="4.92064" AvgRowSize="65" Parallel="0">
                      <OutputList />
                      <Hash>
                        <DefinedValues />
                        <HashKeysBuild>
                          <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[HeapTable]" Alias="[h]" Column="Id" />
                        </HashKeysBuild>
                        <HashKeysProbe>
                          <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[ClusteredTable]" Alias="[c]" Column="Id" />
                        </HashKeysProbe>
                        <RelOp NodeId="1" PhysicalOp="Index Seek" LogicalOp="Index Seek"
                               EstimatedTotalSubtreeCost="0.0032874" EstimateRows="4.92059" AvgRowSize="11" Parallel="0">
                          <OutputList />
                          <IndexScan Ordered="true" ScanDirection="FORWARD" ForceSeek="false">
                            <DefinedValues />
                            <Object Database="[Demo]" Schema="[dbo]" Table="[HeapTable]" Index="[ix_HeapTable_Id]"
                                    Alias="[h]" IndexKind="NonClustered" />
                            <SeekPredicates>
                              <SeekPredicateNew>
                                <SeekKeys>
                                  <EndRange ScanType="LT">
                                    <RangeColumns>
                                      <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[HeapTable]" Alias="[h]" Column="Id" />
                                    </RangeColumns>
                                    <RangeExpressions>
                                      <ScalarOperator ScalarString="(100)">
                                        <Const ConstValue="(100)" />
                                      </ScalarOperator>
                                    </RangeExpressions>
                                  </EndRange>
                                </SeekKeys>
                              </SeekPredicateNew>
                            </SeekPredicates>
                          </IndexScan>
                        </RelOp>
                        <RelOp NodeId="2" PhysicalOp="Clustered Index Seek" LogicalOp="Clustered Index Seek"
                               EstimatedTotalSubtreeCost="0.0033909" EstimateRows="99" AvgRowSize="65" Parallel="0">
                          <OutputList />
                          <IndexScan Ordered="true" ScanDirection="FORWARD" ForceSeek="false">
                            <DefinedValues />
                            <Object Database="[Demo]" Schema="[dbo]" Table="[ClusteredTable]" Index="[pk_ClusteredTable]"
                                    Alias="[c]" IndexKind="Clustered" />
                            <SeekPredicates>
                              <SeekPredicateNew>
                                <SeekKeys>
                                  <EndRange ScanType="LT">
                                    <RangeColumns>
                                      <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[ClusteredTable]" Alias="[c]" Column="Id" />
                                    </RangeColumns>
                                    <RangeExpressions>
                                      <ScalarOperator ScalarString="(100)">
                                        <Const ConstValue="(100)" />
                                      </ScalarOperator>
                                    </RangeExpressions>
                                  </EndRange>
                                </SeekKeys>
                              </SeekPredicateNew>
                            </SeekPredicates>
                          </IndexScan>
                        </RelOp>
                      </Hash>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void The_Parser_Bounds_A_Seek_That_Only_Has_An_End_Range()
    {
        var seek = Node(1);

        Assert.NotNull(seek.PredicateInfo);
        Assert.True(seek.PredicateInfo!.HasSeekBounds);

        var bounds = Assert.Single(seek.PredicateInfo.SeekBounds);

        Assert.Equal("100", bounds.EndValue.ToString());
        Assert.False(bounds.IsEndInclusive);
    }

    [Fact]
    public void Each_Hash_Side_Keeps_Its_Own_Seek_Range()
    {
        var definition = Build(Node(0));

        var hash = Assert.IsType<HashMatchDefinition>(definition);

        var build = Assert.IsType<RangeDefinition>(hash.Build.Source);

        var probe = Assert.IsType<RangeDefinition>(hash.Probe.Source);

        Assert.Equal("100", Assert.Single(build.Ranges).EndValue.ToString());
        Assert.Equal("100", Assert.Single(probe.Ranges).EndValue.ToString());
    }

    [Fact]
    public void Each_Hash_Side_Is_Identified_By_Its_Own_Plan_Node()
    {
        var hash = Assert.IsType<HashMatchDefinition>(Build(Node(0)));

        Assert.Equal(0, hash.NodeId);
        Assert.Equal(1, hash.Build.Source.NodeId);
        Assert.Equal(2, hash.Probe.Source.NodeId);
    }

    private static IteratorDefinition? Build(PlanNode node)
        => new TraceDefinitionBuilder(ResolveUnit).Build(node);

    private static PlanNode Node(int nodeId)
        => ExecutionPlanParser.Parse(HashMatchOfTwoSeeksXml, new PlanHandleRegistry()).NodesById[nodeId];

    /// <summary>
    /// Stands in for the database, giving every named object an allocation unit with a non zero index id
    /// </summary>
    private static AllocationUnit? ResolveUnit(PlanNode node)
        => string.IsNullOrEmpty(node.Table)
            ? null
            : new AllocationUnit
            {
                AllocationUnitId = node.NodeId,
                IndexId = 1,
                RootPage = new PageAddress(1, 100 + node.NodeId),
                TableName = node.Table.Trim('[', ']'),
                IndexName = (node.Index ?? string.Empty).Trim('[', ']')
            };
}
