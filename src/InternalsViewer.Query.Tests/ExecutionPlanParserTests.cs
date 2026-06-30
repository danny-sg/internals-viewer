using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query.Tests;

/// <summary>
/// Unit tests for <see cref="ExecutionPlanParser"/>.
/// XML literals use the real showplan namespace so that namespace-stripping bugs are caught here.
/// </summary>
public class ExecutionPlanParserTests
{
    private const string Ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    private static ExecutionPlan Parse(string xml)
        => ExecutionPlanParser.Parse(xml, new PlanHandleRegistry());

    // ------------------------------------------------------------------
    // Single Clustered Index Seek
    // ------------------------------------------------------------------

    private const string ClusteredIndexSeekXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementType="SELECT" StatementSubTreeCost="0.0032875">
                  <QueryPlan>
                    <RelOp NodeId="0"
                           PhysicalOp="Clustered Index Seek"
                           LogicalOp="Clustered Index Seek"
                           EstimatedTotalSubtreeCost="0.0032875"
                           EstimateRows="5"
                           AvgRowSize="11"
                           Parallel="0">
                      <OutputList>
                        <ColumnReference Database="[AdventureWorks]" Schema="[Production]"
                                         Table="[Product]" Column="ProductID"/>
                      </OutputList>
                      <RunTimeInformation>
                        <RunTimeCountersPerThread Thread="0"
                                                  ActualRows="5"
                                                  ActualRowsRead="5"
                                                  ActualElapsedms="1"
                                                  ActualExecutions="1"
                                                  ActualEndOfScans="1"/>
                      </RunTimeInformation>
                      <IndexScan Ordered="1" ScanDirection="FORWARD" ForcedIndex="0"
                                 ForceSeek="0" ForceScan="0" NoExpandHint="0"
                                 Storage="RowStore">
                        <DefinedValues>
                          <DefinedValue>
                            <ColumnReference Database="[AdventureWorks]" Schema="[Production]"
                                             Table="[Product]" Column="ProductID"/>
                          </DefinedValue>
                        </DefinedValues>
                        <Object Database="[AdventureWorks]" Schema="[Production]"
                                Table="[Product]" Index="[PK_Product_ProductID]"
                                Alias="[p]" IndexKind="Clustered"/>
                      </IndexScan>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void ClusteredIndexSeek_Parses_Operator_Names()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var node = plan.NodesById[0];

        Assert.Equal("Clustered Index Seek", node.PhysicalOperator);
        Assert.Equal("Clustered Index Seek", node.LogicalOperator);
    }

    [Fact]
    public void ClusteredIndexSeek_Parses_Table_And_Index()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var node = plan.NodesById[0];

        Assert.Equal("Production", node.Schema);
        Assert.Equal("Product", node.Table);
        Assert.Equal("PK_Product_ProductID", node.Index);
    }

    [Fact]
    public void ClusteredIndexSeek_Parses_EstimatedCost()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var node = plan.NodesById[0];

        Assert.Equal(0.0032875, node.EstimatedCost);
    }

    [Fact]
    public void ClusteredIndexSeek_Parses_RunTime_Counters()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var node = plan.NodesById[0];

        Assert.True(node.CountersByThread.ContainsKey(0));
        Assert.Equal(5, node.CountersByThread[0].RowsProcessed);
    }

    [Fact]
    public void ClusteredIndexSeek_Parses_ScanInfo_Forward_And_Ordered()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var node = plan.NodesById[0];

        Assert.NotNull(node.ScanInfo);
        Assert.True(node.ScanInfo!.IsForward);
        Assert.True(node.ScanInfo.IsOutputOrdered);
    }

    [Fact]
    public void ClusteredIndexSeek_Is_Indexed_In_NodesById()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        Assert.True(plan.NodesById.ContainsKey(0));
        Assert.Same(plan.NodesById[0], plan.Root[0].Children[0]);
    }

    // ------------------------------------------------------------------
    // Table Scan (no run-time information, no IndexScan child)
    // ------------------------------------------------------------------

    private const string TableScanXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementType="SELECT" StatementSubTreeCost="1.5">
                  <QueryPlan>
                    <RelOp NodeId="0"
                           PhysicalOp="Table Scan"
                           LogicalOp="Table Scan"
                           EstimatedTotalSubtreeCost="1.5"
                           EstimateRows="1000"
                           Parallel="0">
                      <OutputList>
                        <ColumnReference Database="[db]" Schema="[dbo]"
                                         Table="[Heap]" Column="Id"/>
                      </OutputList>
                      <TableScan Ordered="0" ForceScan="0" NoExpandHint="0">
                        <Object Database="[db]" Schema="[dbo]" Table="[Heap]"
                                IndexKind="Heap"/>
                      </TableScan>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void TableScan_Has_No_RunTime_Counters_When_None_Present()
    {
        var plan = Parse(TableScanXml);

        var node = plan.NodesById[0];

        Assert.Empty(node.CountersByThread);
        Assert.Equal(0, node.RowsProcessed);
    }

    [Fact]
    public void TableScan_Parses_Table_With_No_Index()
    {
        var plan = Parse(TableScanXml);

        var node = plan.NodesById[0];

        Assert.Equal("dbo", node.Schema);
        Assert.Equal("Heap", node.Table);
        Assert.Null(node.Index);
    }

    // ------------------------------------------------------------------
    // Hash Match join — two child RelOps, HashInfo build/probe keys
    // ------------------------------------------------------------------

    private const string HashMatchXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementType="SELECT" StatementSubTreeCost="0.5">
                  <QueryPlan>
                    <RelOp NodeId="0"
                           PhysicalOp="Hash Match"
                           LogicalOp="Inner Join"
                           EstimatedTotalSubtreeCost="0.5"
                           Parallel="0">
                      <OutputList/>
                      <Hash>
                        <HashKeysBuild>
                          <ColumnReference Database="[db]" Schema="[dbo]"
                                           Table="[Orders]" Column="CustomerId"/>
                        </HashKeysBuild>
                        <HashKeysProbe>
                          <ColumnReference Database="[db]" Schema="[dbo]"
                                           Table="[Customers]" Column="CustomerId"/>
                        </HashKeysProbe>
                        <RelOp NodeId="1"
                               PhysicalOp="Clustered Index Scan"
                               LogicalOp="Clustered Index Scan"
                               EstimatedTotalSubtreeCost="0.2"
                               Parallel="0">
                          <OutputList/>
                          <IndexScan Ordered="0" ScanDirection="FORWARD"
                                     ForcedIndex="0" ForceSeek="0" ForceScan="0"
                                     NoExpandHint="0" Storage="RowStore">
                            <Object Database="[db]" Schema="[dbo]"
                                    Table="[Orders]" Index="[PK_Orders]"
                                    IndexKind="Clustered"/>
                          </IndexScan>
                        </RelOp>
                        <RelOp NodeId="2"
                               PhysicalOp="Clustered Index Scan"
                               LogicalOp="Clustered Index Scan"
                               EstimatedTotalSubtreeCost="0.1"
                               Parallel="0">
                          <OutputList/>
                          <IndexScan Ordered="0" ScanDirection="FORWARD"
                                     ForcedIndex="0" ForceSeek="0" ForceScan="0"
                                     NoExpandHint="0" Storage="RowStore">
                            <Object Database="[db]" Schema="[dbo]"
                                    Table="[Customers]" Index="[PK_Customers]"
                                    IndexKind="Clustered"/>
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
    public void HashMatch_Parses_Two_Child_Operators()
    {
        var plan = Parse(HashMatchXml);

        var root = plan.NodesById[0];

        Assert.Equal(2, root.Children.Count);
        Assert.True(plan.NodesById.ContainsKey(1));
        Assert.True(plan.NodesById.ContainsKey(2));
    }

    [Fact]
    public void HashMatch_Parses_Build_And_Probe_Keys()
    {
        var plan = Parse(HashMatchXml);

        var root = plan.NodesById[0];

        Assert.NotNull(root.HashInfo);

        var build = Assert.Single(root.HashInfo!.BuildKeys);
        Assert.Equal("CustomerId", build.Column);
        Assert.Equal("Orders", build.Table);

        var probe = Assert.Single(root.HashInfo.ProbeKeys);
        Assert.Equal("CustomerId", probe.Column);
        Assert.Equal("Customers", probe.Table);
    }

    [Fact]
    public void HashMatch_Children_Have_Correct_Tables()
    {
        var plan = Parse(HashMatchXml);

        Assert.Equal("Orders", plan.NodesById[1].Table);
        Assert.Equal("Customers", plan.NodesById[2].Table);
    }

    // ------------------------------------------------------------------
    // Statement node wrapping
    // ------------------------------------------------------------------

    [Fact]
    public void Root_Contains_Statement_Node_Wrapping_RelOp()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var statement = Assert.Single(plan.Root);

        Assert.True(statement.IsStatement);
        Assert.Equal(-1, statement.NodeId);
        Assert.Single(statement.Children);
    }

    [Fact]
    public void Statement_Node_Cost_Comes_From_StatementSubTreeCost()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        Assert.Equal(0.0032875, plan.Root[0].EstimatedCost);
    }
}
