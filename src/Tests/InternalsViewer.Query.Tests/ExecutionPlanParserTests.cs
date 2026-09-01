
using System.Data;
using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Windowing;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Parsers;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query.Tests;

/// <summary>
/// Unit tests for <see cref="ExecutionPlanParser"/>.
/// XML literals use the real showplan namespace so that namespace-stripping bugs are caught here.
/// </summary>
[Trait("Category", "Unit")]
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
    public void ClusteredIndexSeek_Parses_EstimatedRows()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var node = plan.NodesById[0];

        Assert.Equal(5, node.EstimatedRows);
    }

    [Fact]
    public void EstimateRows_Is_Rounded_To_The_Nearest_Row()
    {
        var plan = Parse(
            """
            <?xml version="1.0"?>
            <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                         Version="1.7" Build="16.0.0">
              <BatchSequence>
                <Batch>
                  <Statements>
                    <StmtSimple StatementType="SELECT" StatementSubTreeCost="0.1">
                      <QueryPlan>
                        <RelOp NodeId="0"
                               PhysicalOp="Clustered Index Scan"
                               LogicalOp="Clustered Index Scan"
                               EstimatedTotalSubtreeCost="0.1"
                               EstimateRows="9.99999"
                               Parallel="0">
                          <IndexScan Ordered="0" ForcedIndex="0" ForceSeek="0" ForceScan="0"
                                     NoExpandHint="0" Storage="RowStore">
                            <Object Database="[db]" Schema="[dbo]"
                                    Table="[ClusteredTable]" Index="[PK_ClusteredTable]"
                                    TableReferenceId="-1" IndexKind="Clustered"/>
                          </IndexScan>
                        </RelOp>
                      </QueryPlan>
                    </StmtSimple>
                  </Statements>
                </Batch>
              </BatchSequence>
            </ShowPlanXML>
            """);

        Assert.Equal(10, plan.NodesById[0].EstimatedRows);
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
    // Key Lookup — a Clustered Index Seek with Lookup="1", renamed as SSMS does
    // ------------------------------------------------------------------

    private const string KeyLookupXml =
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
                           EstimateRows="1"
                           Parallel="0">
                      <OutputList>
                        <ColumnReference Database="[db]" Schema="[dbo]"
                                         Table="[ClusteredTable]" Column="CreatedDate"/>
                      </OutputList>
                      <IndexScan Lookup="1" Ordered="1" ScanDirection="FORWARD"
                                 ForcedIndex="0" ForceSeek="0" ForceScan="0"
                                 NoExpandHint="0" Storage="RowStore">
                        <Object Database="[db]" Schema="[dbo]"
                                Table="[ClusteredTable]" Index="[PK_ClusteredTable]"
                                TableReferenceId="-1" IndexKind="Clustered"/>
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
    public void ClusteredIndexSeek_With_Lookup_Is_Renamed_To_Key_Lookup()
    {
        var plan = Parse(KeyLookupXml);

        var node = plan.NodesById[0];

        Assert.Equal("Key Lookup", node.PhysicalOperator);
        Assert.NotNull(node.ScanInfo);
        Assert.True(node.ScanInfo!.IsLookup);
    }

    [Fact]
    public void ClusteredIndexSeek_Without_Lookup_Keeps_Its_Name()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        var node = plan.NodesById[0];

        Assert.Equal("Clustered Index Seek", node.PhysicalOperator);
        Assert.False(node.ScanInfo!.IsLookup);
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

    [Fact]
    public void HashMatch_Root_Has_No_Table_Of_Its_Own()
    {
        // Regression test: a naive Descendants() search for <Object> would walk into the child RelOps
        // and pick up the first child's table (Orders), wrongly attributing it to the join itself.
        var plan = Parse(HashMatchXml);

        var root = plan.NodesById[0];

        Assert.Null(root.Table);
        Assert.Null(root.Schema);
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

    // ------------------------------------------------------------------
    // Range seek predicates
    // ------------------------------------------------------------------

    private const string RangeSeekXml =
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
                           EstimateRows="399"
                           AvgRowSize="11"
                           Parallel="0">
                      <OutputList>
                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                         Table="[ClusteredTable]" Column="Id"/>
                      </OutputList>
                      <IndexScan Ordered="1" ScanDirection="FORWARD" ForcedIndex="0"
                                 ForceSeek="0" ForceScan="0" NoExpandHint="0"
                                 Storage="RowStore">
                        <DefinedValues>
                          <DefinedValue>
                            <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                             Table="[ClusteredTable]" Column="Id"/>
                          </DefinedValue>
                        </DefinedValues>
                        <Object Database="[InternalsViewerDemo]" Schema="[dbo]"
                                Table="[ClusteredTable]" Index="[PK_ClusteredTable]"
                                IndexKind="Clustered"/>
                        <SeekPredicates>
                          <SeekPredicateNew>
                            <SeekKeys>
                              <StartRange ScanType="GT">
                                <RangeColumns>
                                  <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                   Table="[ClusteredTable]" Column="Id"/>
                                </RangeColumns>
                                <RangeExpressions>
                                  <ScalarOperator ScalarString="CONVERT_IMPLICIT(int,[@1],0)">
                                    <Convert DataType="int" Style="0" Implicit="1">
                                      <ScalarOperator>
                                        <Identifier>
                                          <ColumnReference Column="@1"/>
                                        </Identifier>
                                      </ScalarOperator>
                                    </Convert>
                                  </ScalarOperator>
                                </RangeExpressions>
                              </StartRange>
                              <EndRange ScanType="LT">
                                <RangeColumns>
                                  <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                   Table="[ClusteredTable]" Column="Id"/>
                                </RangeColumns>
                                <RangeExpressions>
                                  <ScalarOperator ScalarString="CONVERT_IMPLICIT(int,[@2],0)">
                                    <Convert DataType="int" Style="0" Implicit="1">
                                      <ScalarOperator>
                                        <Identifier>
                                          <ColumnReference Column="@2"/>
                                        </Identifier>
                                      </ScalarOperator>
                                    </Convert>
                                  </ScalarOperator>
                                </RangeExpressions>
                              </EndRange>
                            </SeekKeys>
                          </SeekPredicateNew>
                        </SeekPredicates>
                      </IndexScan>
                    </RelOp>
                    <ParameterList>
                      <ColumnReference Column="@1" ParameterCompiledValue="(100)"/>
                      <ColumnReference Column="@2" ParameterCompiledValue="(500)"/>
                    </ParameterList>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    private const string ScanWithResidualXml =
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
                           PhysicalOp="Clustered Index Scan"
                           LogicalOp="Clustered Index Scan"
                           EstimatedTotalSubtreeCost="0.5"
                           EstimateRows="100"
                           AvgRowSize="11"
                           Parallel="0">
                      <OutputList>
                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                         Table="[ClusteredTable]" Column="Id"/>
                      </OutputList>
                      <IndexScan Ordered="0" ForcedIndex="0" ForceScan="0" NoExpandHint="0"
                                 Storage="RowStore">
                        <DefinedValues>
                          <DefinedValue>
                            <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                             Table="[ClusteredTable]" Column="Id"/>
                          </DefinedValue>
                        </DefinedValues>
                        <Object Database="[InternalsViewerDemo]" Schema="[dbo]"
                                Table="[ClusteredTable]" Index="[PK_ClusteredTable]"
                                IndexKind="Clustered"/>
                        <Predicate>
                          <ScalarOperator ScalarString="[InternalsViewerDemo].[dbo].[ClusteredTable].[Id]&lt;=(100)">
                            <Compare CompareOp="LE">
                              <ScalarOperator>
                                <Identifier>
                                  <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                   Table="[ClusteredTable]" Column="Id"/>
                                </Identifier>
                              </ScalarOperator>
                              <ScalarOperator>
                                <Const ConstValue="(100)"/>
                              </ScalarOperator>
                            </Compare>
                          </ScalarOperator>
                        </Predicate>
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
    public void Scan_Residual_Predicate_Inside_IndexScan_Is_Found()
    {
        var plan = Parse(ScanWithResidualXml);

        var node = plan.NodesById[0];

        Assert.NotNull(node.PredicateInfo);
        Assert.False(node.PredicateInfo!.HasSeekBounds);
        Assert.NotNull(node.PredicateInfo.Residual);
        Assert.False(node.PredicateInfo.HasUntranslatedPredicate);
    }

    private const string TopOverScanXml =
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
                           PhysicalOp="Top"
                           LogicalOp="Top"
                           EstimatedTotalSubtreeCost="0.5"
                           EstimateRows="10"
                           AvgRowSize="73"
                           Parallel="0">
                      <OutputList>
                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                         Table="[ClusteredTable]" Column="Id"/>
                      </OutputList>
                      <Top RowCount="0" IsPercent="false" WithTies="false">
                        <TopExpression>
                          <ScalarOperator ScalarString="(10)">
                            <Const ConstValue="(10)"/>
                          </ScalarOperator>
                        </TopExpression>
                        <RelOp NodeId="1"
                               PhysicalOp="Clustered Index Scan"
                               LogicalOp="Clustered Index Scan"
                               EstimatedTotalSubtreeCost="0.4"
                               EstimateRows="10"
                               AvgRowSize="73"
                               Parallel="0">
                          <OutputList>
                            <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                             Table="[ClusteredTable]" Column="Id"/>
                          </OutputList>
                          <IndexScan Ordered="0" ForcedIndex="0" ForceScan="0" NoExpandHint="0"
                                     Storage="RowStore">
                            <DefinedValues>
                              <DefinedValue>
                                <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                 Table="[ClusteredTable]" Column="Id"/>
                              </DefinedValue>
                            </DefinedValues>
                            <Object Database="[InternalsViewerDemo]" Schema="[dbo]"
                                    Table="[ClusteredTable]" Index="[PK_ClusteredTable]"
                                    IndexKind="Clustered"/>
                          </IndexScan>
                        </RelOp>
                      </Top>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void Top_Over_A_Scan_Sets_The_Row_Goal()
    {
        var plan = Parse(TopOverScanXml);

        var scan = plan.NodesById[1];

        Assert.NotNull(scan.PredicateInfo);
        Assert.Equal(10, scan.PredicateInfo!.RowGoal);
    }

    [Fact]
    public void Range_Seek_Predicate_Produces_Seek_Bounds()
    {
        var plan = Parse(RangeSeekXml);

        var node = plan.NodesById[0];

        Assert.NotNull(node.PredicateInfo);
        Assert.True(node.PredicateInfo!.HasSeekBounds);

        var bounds = Assert.Single(node.PredicateInfo.SeekBounds);

        Assert.False(bounds.IsStartInclusive);
        Assert.False(bounds.IsEndInclusive);
        Assert.Equal(100, bounds.StartValue.Values[0].Numeric);
        Assert.Equal(500, bounds.EndValue.Values[0].Numeric);
    }

    // ------------------------------------------------------------------
    // Memory grants — query level MemoryGrantInfo and per thread operator grants
    // ------------------------------------------------------------------

    private const string MemoryGrantXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementType="SELECT" StatementSubTreeCost="1.5">
                  <QueryPlan MemoryGrant="1408">
                    <MemoryGrantInfo SerialRequiredMemory="512"
                                     SerialDesiredMemory="1408"
                                     RequiredMemory="1024"
                                     DesiredMemory="1920"
                                     RequestedMemory="1920"
                                     GrantWaitTime="3"
                                     GrantedMemory="1920"
                                     MaxUsedMemory="704"
                                     MaxQueryMemory="828184"/>
                    <RelOp NodeId="0"
                           PhysicalOp="Sort"
                           LogicalOp="Sort"
                           EstimatedTotalSubtreeCost="1.5"
                           EstimateRows="1000"
                           Parallel="1">
                      <RunTimeInformation>
                        <RunTimeCountersPerThread Thread="1"
                                                  ActualRows="500"
                                                  ActualElapsedms="12"
                                                  InputMemoryGrant="640"
                                                  OutputMemoryGrant="576"
                                                  UsedMemoryGrant="192"/>
                        <RunTimeCountersPerThread Thread="2"
                                                  ActualRows="500"
                                                  ActualElapsedms="14"
                                                  InputMemoryGrant="640"
                                                  OutputMemoryGrant="576"
                                                  UsedMemoryGrant="160"/>
                      </RunTimeInformation>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void Operator_Memory_Grants_Are_Summed_Across_Threads()
    {
        var plan = Parse(MemoryGrantXml);

        var node = plan.NodesById[0];

        Assert.NotNull(node.MemoryGrant);
        Assert.Equal(1280, node.MemoryGrant!.InputKb);
        Assert.Equal(1152, node.MemoryGrant.OutputKb);
        Assert.Equal(352, node.MemoryGrant.UsedKb);
    }

    [Fact]
    public void Query_Memory_Grant_Is_Parsed_Onto_The_Statement_Node()
    {
        var plan = Parse(MemoryGrantXml);

        var grant = plan.Root[0].QueryMemoryGrant;

        Assert.NotNull(grant);
        Assert.Equal(512, grant!.SerialRequiredKb);
        Assert.Equal(1408, grant.SerialDesiredKb);
        Assert.Equal(1024, grant.RequiredKb);
        Assert.Equal(1920, grant.DesiredKb);
        Assert.Equal(1920, grant.RequestedKb);
        Assert.Equal(1920, grant.GrantedKb);
        Assert.Equal(704, grant.MaxUsedKb);
        Assert.Equal(828184, grant.MaxQueryKb);
        Assert.Equal(3, grant.GrantWaitTimeSeconds);
    }

    [Fact]
    public void An_Operator_Without_Memory_Grant_Counters_Has_No_Memory_Grant()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        Assert.Null(plan.NodesById[0].MemoryGrant);
    }

    [Fact]
    public void A_Plan_Without_MemoryGrantInfo_Has_No_Query_Memory_Grant()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        Assert.Null(plan.Root[0].QueryMemoryGrant);
    }

    // ------------------------------------------------------------------
    // Stream Aggregate under a Compute Scalar
    // ------------------------------------------------------------------

    private const string StreamAggregateXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementType="SELECT" StatementSubTreeCost="0.0265598">
                  <QueryPlan>
                    <RelOp NodeId="0" PhysicalOp="Compute Scalar" LogicalOp="Compute Scalar"
                           EstimateRows="1" EstimatedTotalSubtreeCost="0.0265598">
                      <OutputList>
                        <ColumnReference Column="Expr1003" />
                        <ColumnReference Column="Expr1004" />
                        <ColumnReference Column="Expr1005" />
                      </OutputList>
                      <ComputeScalar>
                        <DefinedValues>
                          <DefinedValue>
                            <ColumnReference Column="Expr1005" />
                            <ScalarOperator ScalarString="CONVERT_IMPLICIT(int,[Expr1010],0)">
                              <Convert DataType="int" Style="0" Implicit="1">
                                <ScalarOperator>
                                  <Identifier>
                                    <ColumnReference Column="Expr1010" />
                                  </Identifier>
                                </ScalarOperator>
                              </Convert>
                            </ScalarOperator>
                          </DefinedValue>
                        </DefinedValues>
                        <RelOp NodeId="1" PhysicalOp="Stream Aggregate" LogicalOp="Aggregate"
                               EstimateRows="1" EstimatedTotalSubtreeCost="0.0265598">
                          <OutputList>
                            <ColumnReference Column="Expr1003" />
                            <ColumnReference Column="Expr1004" />
                            <ColumnReference Column="Expr1010" />
                          </OutputList>
                          <StreamAggregate>
                            <DefinedValues>
                              <DefinedValue>
                                <ColumnReference Column="Expr1010" />
                                <ScalarOperator ScalarString="Count(*)">
                                  <Aggregate AggType="countstar" Distinct="false" />
                                </ScalarOperator>
                              </DefinedValue>
                              <DefinedValue>
                                <ColumnReference Column="Expr1003" />
                                <ScalarOperator ScalarString="MIN([InternalsViewerDemo].[dbo].[HeapTable].[Id])">
                                  <Aggregate AggType="MIN" Distinct="false">
                                    <ScalarOperator>
                                      <Identifier>
                                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                         Table="[HeapTable]" Column="Id" />
                                      </Identifier>
                                    </ScalarOperator>
                                  </Aggregate>
                                </ScalarOperator>
                              </DefinedValue>
                              <DefinedValue>
                                <ColumnReference Column="Expr1004" />
                                <ScalarOperator ScalarString="MAX([InternalsViewerDemo].[dbo].[HeapTable].[Id])">
                                  <Aggregate AggType="MAX" Distinct="false">
                                    <ScalarOperator>
                                      <Identifier>
                                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                         Table="[HeapTable]" Column="Id" />
                                      </Identifier>
                                    </ScalarOperator>
                                  </Aggregate>
                                </ScalarOperator>
                              </DefinedValue>
                            </DefinedValues>
                            <RelOp NodeId="2" PhysicalOp="Table Scan" LogicalOp="Table Scan"
                                   EstimateRows="5000" EstimatedTotalSubtreeCost="0.0265598"
                                   TableCardinality="5000">
                              <OutputList>
                                <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                 Table="[HeapTable]" Column="Id" />
                              </OutputList>
                              <TableScan Ordered="false" ForcedIndex="false" ForceScan="false"
                                         NoExpandHint="false" Storage="RowStore">
                                <DefinedValues>
                                  <DefinedValue>
                                    <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                                     Table="[HeapTable]" Column="Id" />
                                  </DefinedValue>
                                </DefinedValues>
                                <Object Database="[InternalsViewerDemo]" Schema="[dbo]" Table="[HeapTable]"
                                        IndexKind="Heap" Storage="RowStore" />
                              </TableScan>
                            </RelOp>
                          </StreamAggregate>
                        </RelOp>
                      </ComputeScalar>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void Stream_Aggregate_Parses_Every_Aggregate_It_Defines()
    {
        var plan = Parse(StreamAggregateXml);

        var info = plan.NodesById[1].AggregateInfo;

        Assert.NotNull(info);
        Assert.True(info!.IsScalar);
        Assert.False(info.HasUntranslatedAggregate);

        Assert.Equal(["Expr1010", "Expr1003", "Expr1004"], info.Columns.Select(c => c.Column));

        Assert.Equal([AggregateFunction.CountStar, AggregateFunction.Min, AggregateFunction.Max],
                     info.Columns.Select(c => c.Function));

        Assert.All(info.Columns, column => Assert.False(column.IsDistinct));
    }

    [Fact]
    public void An_Aggregate_Argument_Is_Parsed_As_The_Column_It_Reads()
    {
        var plan = Parse(StreamAggregateXml);

        var minimum = plan.NodesById[1].AggregateInfo!.Columns.Single(c => c.Function == AggregateFunction.Min);

        Assert.Equal("Id", Assert.IsType<AccessExpression.Column>(minimum.Argument).Name);

        Assert.Null(plan.NodesById[1].AggregateInfo!.Columns.Single(c => c.Function == AggregateFunction.CountStar).Argument);
    }

    [Fact]
    public void A_Compute_Scalar_Keeps_The_Type_Its_Convert_Names()
    {
        var plan = Parse(StreamAggregateXml);

        var definedValue = Assert.Single(plan.NodesById[0].DefinedValues);

        Assert.Equal(SqlDbType.Int, definedValue.DataType);

        Assert.Equal("Expr1010", Assert.IsType<AccessExpression.Column>(definedValue.ParsedExpression).Name);
    }

    // ------------------------------------------------------------------
    // Filter over an aggregate, which is the shape HAVING takes
    // ------------------------------------------------------------------

    private const string FilterOverAggregateXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementType="SELECT" StatementSubTreeCost="0.01">
                  <QueryPlan>
                    <RelOp NodeId="0" PhysicalOp="Filter" LogicalOp="Filter" EstimateRows="10"
                           EstimatedTotalSubtreeCost="0.01">
                      <OutputList>
                        <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[DuplicateKeyTable]" Column="Category" />
                        <ColumnReference Column="Expr1003" />
                      </OutputList>
                      <Filter StartupExpression="false">
                        <RelOp NodeId="1" PhysicalOp="Stream Aggregate" LogicalOp="Aggregate" EstimateRows="20"
                               EstimatedTotalSubtreeCost="0.01">
                          <OutputList>
                            <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[DuplicateKeyTable]" Column="Category" />
                            <ColumnReference Column="Expr1003" />
                          </OutputList>
                          <StreamAggregate>
                            <DefinedValues>
                              <DefinedValue>
                                <ColumnReference Column="Expr1003" />
                                <ScalarOperator ScalarString="COUNT(*)">
                                  <Aggregate AggType="COUNT*" Distinct="false" />
                                </ScalarOperator>
                              </DefinedValue>
                            </DefinedValues>
                            <GroupBy>
                              <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[DuplicateKeyTable]" Column="Category" />
                            </GroupBy>
                            <RelOp NodeId="2" PhysicalOp="Clustered Index Scan" LogicalOp="Clustered Index Scan"
                                   EstimateRows="2000" EstimatedTotalSubtreeCost="0.01">
                              <OutputList>
                                <ColumnReference Database="[Demo]" Schema="[dbo]" Table="[DuplicateKeyTable]" Column="Category" />
                              </OutputList>
                              <IndexScan Ordered="true" ScanDirection="FORWARD">
                                <DefinedValues />
                                <Object Database="[Demo]" Schema="[dbo]" Table="[DuplicateKeyTable]"
                                        Index="[ix_DuplicateKeyTable_Category]" IndexKind="Clustered" />
                              </IndexScan>
                            </RelOp>
                          </StreamAggregate>
                        </RelOp>
                        <Predicate>
                          <ScalarOperator ScalarString="[Expr1003]&gt;(50)">
                            <Compare CompareOp="GT">
                              <ScalarOperator>
                                <Identifier>
                                  <ColumnReference Column="Expr1003" />
                                </Identifier>
                              </ScalarOperator>
                              <ScalarOperator>
                                <Const ConstValue="(50)" />
                              </ScalarOperator>
                            </Compare>
                          </ScalarOperator>
                        </Predicate>
                      </Filter>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void A_Filter_Translates_A_Predicate_Over_A_Column_The_Plan_Invented()
    {
        var plan = Parse(FilterOverAggregateXml);

        var predicateInfo = plan.NodesById[0].PredicateInfo;

        Assert.NotNull(predicateInfo);
        Assert.False(predicateInfo!.HasUntranslatedPredicate);

        var comparison = Assert.IsType<AccessPredicate.Comparison>(predicateInfo.Residual);

        var column = Assert.IsType<AccessExpression.Column>(comparison.Left);

        Assert.Equal("Expr1003", column.Name);
        Assert.Equal(-1, column.Ordinal);
    }

    // ------------------------------------------------------------------
    // Sequence Project over Segment, from ROW_NUMBER() OVER (ORDER BY Id)
    // ------------------------------------------------------------------
    private const string RowNumberXml =
        """
        <?xml version="1.0" encoding="utf-16"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementText="SELECT ROW_NUMBER() OVER (ORDER BY Id) FROM dbo.ClusteredTable" StatementType="SELECT">
                  <QueryPlan>
                    <RelOp NodeId="0" PhysicalOp="Sequence Project" LogicalOp="Compute Scalar" EstimateRows="100">
                      <OutputList>
                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]" Table="[ClusteredTable]" Column="Id" />
                        <ColumnReference Column="Expr1002" />
                      </OutputList>
                      <SequenceProject>
                        <DefinedValues>
                          <DefinedValue>
                            <ColumnReference Column="Expr1002" />
                            <ScalarOperator ScalarString="row_number">
                              <Sequence FunctionName="row_number" />
                            </ScalarOperator>
                          </DefinedValue>
                        </DefinedValues>
                        <RelOp NodeId="1" PhysicalOp="Segment" LogicalOp="Segment" EstimateRows="100">
                          <OutputList>
                            <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]" Table="[ClusteredTable]" Column="Id" />
                            <ColumnReference Column="Segment1003" />
                          </OutputList>
                          <Segment>
                            <GroupBy />
                            <SegmentColumn>
                              <ColumnReference Column="Segment1003" />
                            </SegmentColumn>
                            <RelOp NodeId="2" PhysicalOp="Clustered Index Seek" LogicalOp="Clustered Index Seek" EstimateRows="100">
                              <OutputList>
                                <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]" Table="[ClusteredTable]" Column="Id" />
                              </OutputList>
                              <IndexScan Ordered="true" ScanDirection="FORWARD">
                                <Object Database="[InternalsViewerDemo]" Schema="[dbo]" Table="[ClusteredTable]" Index="[pk_ClusteredTable]" IndexKind="Clustered" />
                              </IndexScan>
                            </RelOp>
                          </Segment>
                        </RelOp>
                      </SequenceProject>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void A_Segment_Reads_Its_Flag_Column_And_Its_Grouping_Columns()
    {
        var plan = Parse(RowNumberXml);

        var segment = plan.NodesById[1].SegmentInfo;

        Assert.NotNull(segment);
        Assert.Equal("Segment1003", segment!.SegmentColumn?.Column);

        // OVER (ORDER BY Id) has no PARTITION BY, so showplan writes an empty GroupBy and the whole input is one segment
        Assert.Empty(segment.GroupBy);
    }

    [Fact]
    public void A_Sequence_Project_Reads_The_Ranking_Function_It_Defines()
    {
        var plan = Parse(RowNumberXml);

        var sequence = plan.NodesById[0].SequenceProjectInfo;

        Assert.NotNull(sequence);
        Assert.False(sequence!.HasUntranslatedFunction);

        var column = Assert.Single(sequence.Columns);

        Assert.Equal("Expr1002", column.Column);
        Assert.Equal(RankingFunction.RowNumber, column.Function);
    }

    [Fact]
    public void A_Ranking_Function_That_Is_Not_Recognised_Leaves_The_Operator_Untranslated()
    {
        var plan = Parse(RowNumberXml.Replace("row_number", "ntile"));

        var sequence = plan.NodesById[0].SequenceProjectInfo;

        Assert.NotNull(sequence);
        Assert.True(sequence!.HasUntranslatedFunction);
        Assert.Empty(sequence.Columns);
    }

    [Fact]
    public void An_Operator_That_Is_Not_A_Window_Operator_Has_No_Window_Info()
    {
        var plan = Parse(RowNumberXml);

        Assert.Null(plan.NodesById[0].SegmentInfo);
        Assert.Null(plan.NodesById[1].SequenceProjectInfo);
        Assert.Null(plan.NodesById[2].SegmentInfo);
    }

    [Fact]
    public void An_Operator_That_Is_Not_An_Aggregate_Has_No_Aggregate_Info()
    {
        var plan = Parse(StreamAggregateXml);

        Assert.Null(plan.NodesById[0].AggregateInfo);
        Assert.Null(plan.NodesById[2].AggregateInfo);
    }

    // ------------------------------------------------------------------
    // Batch mode columnstore counters
    // ------------------------------------------------------------------

    private const string ColumnstoreBatchXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementText="SELECT Region, COUNT(*) FROM dbo.Sales GROUP BY Region"
                            StatementType="SELECT">
                  <QueryPlan>
                    <RelOp NodeId="0"
                           PhysicalOp="Columnstore Index Scan"
                           LogicalOp="Index Scan"
                           EstimatedTotalSubtreeCost="1"
                           EstimateRows="1685159">
                      <OutputList />
                      <RunTimeInformation>
                        <RunTimeCountersPerThread Thread="0"
                                                  ActualRows="5"
                                                  ActualRowsRead="1685159"
                                                  ActualElapsedms="120"
                                                  ActualExecutions="1"
                                                  ActualExecutionMode="Batch"
                                                  Batches="1880"
                                                  SegmentReads="2"
                                                  SegmentSkips="0"
                                                  ActualLocallyAggregatedRows="1685154" />
                      </RunTimeInformation>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void Locally_Aggregated_Rows_Are_Read_From_The_Scan_Counters()
    {
        var plan = Parse(ColumnstoreBatchXml);

        Assert.Equal(1685154, plan.NodesById[0].BatchInfo!.LocallyAggregatedRows);
    }

    [Fact]
    public void Batch_Counters_Are_Read_Alongside_Locally_Aggregated_Rows()
    {
        var batchInfo = Parse(ColumnstoreBatchXml).NodesById[0].BatchInfo!;

        Assert.Equal(1880, batchInfo.BatchCount);

        Assert.Equal(2, batchInfo.SegmentReads);

        Assert.Equal(0, batchInfo.SegmentSkips);

        Assert.Null(batchInfo.PureRowBuckets);
    }

    // ------------------------------------------------------------------
    // Automatic statistics sampling
    // ------------------------------------------------------------------

    private const string StatManXml =
        """
        <?xml version="1.0"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan"
                     Version="1.7" Build="16.0.0">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementText="SELECT StatMan([SC0]) FROM (SELECT TOP 100 PERCENT [Id] AS [SC0] FROM [dbo].[HeapTable] WITH (READUNCOMMITTED) ORDER BY [SC0] ) AS _MS_UPDSTATS_TBL OPTION (MAXDOP 16)"
                            StatementType="SELECT"
                            StatementSubTreeCost="0.0100024">
                  <QueryPlan>
                    <RelOp NodeId="0"
                           PhysicalOp="Stream Aggregate"
                           LogicalOp="Aggregate"
                           EstimatedTotalSubtreeCost="0.0100024"
                           EstimateRows="1"
                           AvgRowSize="9"
                           Parallel="0">
                      <StreamAggregate />
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void An_Automatic_Statistics_Plan_Is_Marked_Internal()
    {
        var plan = Parse(StatManXml);

        Assert.True(plan.IsInternalPlan);
    }

    [Fact]
    public void A_Submitted_Query_Plan_Is_Not_Marked_Internal()
    {
        var plan = Parse(ClusteredIndexSeekXml);

        Assert.False(plan.IsInternalPlan);
    }
}
