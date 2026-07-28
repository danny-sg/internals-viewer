using System.Xml.Linq;
using InternalsViewer.Query.Parsing.Plans.Predicates;

namespace InternalsViewer.Query.Tests.UnitTests.Parsing.Plans.Predicates;

public class SeekPredicateParserTests
{
    [Fact]
    public void Range_On_Const_Expression_Parameter_Is_Parsed()
    {
        var xml = XElement.Parse(
            """
            <QueryPlan>
              <ParameterList>
                <ColumnReference Column="@2" ParameterDataType="smallint" ParameterCompiledValue="(500)" ParameterRuntimeValue="(500)" />
                <ColumnReference Column="@1" ParameterDataType="tinyint" ParameterCompiledValue="(100)" ParameterRuntimeValue="(100)" />
              </ParameterList>
              <SeekPredicates>
                <SeekPredicateNew>
                  <SeekKeys>
                    <StartRange ScanType="GT">
                      <RangeColumns>
                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]" Table="[ClusteredTable]" Column="Id" />
                      </RangeColumns>
                      <RangeExpressions>
                        <ScalarOperator ScalarString="CONVERT_IMPLICIT(int,[@1],0)">
                          <Identifier>
                            <ColumnReference Column="ConstExpr1002">
                              <ScalarOperator>
                                <Convert DataType="int" Style="0" Implicit="true">
                                  <ScalarOperator>
                                    <Identifier>
                                      <ColumnReference Column="@1" />
                                    </Identifier>
                                  </ScalarOperator>
                                </Convert>
                              </ScalarOperator>
                            </ColumnReference>
                          </Identifier>
                        </ScalarOperator>
                      </RangeExpressions>
                    </StartRange>
                    <EndRange ScanType="LT">
                      <RangeColumns>
                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]" Table="[ClusteredTable]" Column="Id" />
                      </RangeColumns>
                      <RangeExpressions>
                        <ScalarOperator ScalarString="CONVERT_IMPLICIT(int,[@2],0)">
                          <Identifier>
                            <ColumnReference Column="ConstExpr1003">
                              <ScalarOperator>
                                <Convert DataType="int" Style="0" Implicit="true">
                                  <ScalarOperator>
                                    <Identifier>
                                      <ColumnReference Column="@2" />
                                    </Identifier>
                                  </ScalarOperator>
                                </Convert>
                              </ScalarOperator>
                            </ColumnReference>
                          </Identifier>
                        </ScalarOperator>
                      </RangeExpressions>
                    </EndRange>
                  </SeekKeys>
                </SeekPredicateNew>
              </SeekPredicates>
            </QueryPlan>
            """);

        var parameters = PlanParameters.Parse(xml);

        var seekPredicates = xml.Elements().First(e => e.Name.LocalName == "SeekPredicates");

        var parser = new SeekPredicateParser(resolveParameter: parameters.Resolve);

        var bounds = parser.ParseSeekPredicates(seekPredicates);

        var seek = Assert.Single(bounds);

        Assert.False(seek.IsStartInclusive);
        Assert.False(seek.IsEndInclusive);
        Assert.Equal(100, seek.StartValue.Values[0].Numeric);
        Assert.Equal(500, seek.EndValue.Values[0].Numeric);
        Assert.Equal("Id", seek.StartValue.Values[0].ColumnName);
    }

    [Fact]
    public void End_Only_Range_Is_Parsed()
    {
        var xml = XElement.Parse(
            """
            <SeekPredicates>
              <SeekPredicateNew>
                <SeekKeys>
                  <EndRange ScanType="LE">
                    <RangeColumns>
                      <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                       Table="[ClusteredTable]" Column="Id" />
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
            """);

        var parser = new SeekPredicateParser();

        var bounds = parser.ParseSeekPredicates(xml);

        var seek = Assert.Single(bounds);

        Assert.True(seek.StartValue.IsUnbounded);
        Assert.True(seek.IsEndInclusive);
        Assert.Equal(100, seek.EndValue.Values[0].Numeric);
    }

    [Fact]
    public void Start_And_End_Range_Is_Parsed()
    {
        var xml = XElement.Parse(
            """
            <SeekPredicates>
              <SeekPredicateNew>
                <SeekKeys>
                  <StartRange ScanType="GT">
                    <RangeColumns>
                      <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                       Table="[ClusteredTable]" Column="Id" />
                    </RangeColumns>
                    <RangeExpressions>
                      <ScalarOperator ScalarString="(100)">
                        <Const ConstValue="(100)" />
                      </ScalarOperator>
                    </RangeExpressions>
                  </StartRange>
                  <EndRange ScanType="LT">
                    <RangeColumns>
                      <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                       Table="[ClusteredTable]" Column="Id" />
                    </RangeColumns>
                    <RangeExpressions>
                      <ScalarOperator ScalarString="(500)">
                        <Const ConstValue="(500)" />
                      </ScalarOperator>
                    </RangeExpressions>
                  </EndRange>
                </SeekKeys>
              </SeekPredicateNew>
            </SeekPredicates>
            """);

        var parser = new SeekPredicateParser();

        var bounds = parser.ParseSeekPredicates(xml);

        var seek = Assert.Single(bounds);

        Assert.False(seek.IsStartInclusive);
        Assert.False(seek.IsEndInclusive);
        Assert.Equal(100, seek.StartValue.Values[0].Numeric);
        Assert.Equal(500, seek.EndValue.Values[0].Numeric);
    }

    [Fact]
    public void Prefix_Seek_On_Implicitly_Converted_Parameter_Is_Parsed()
    {
        var xml = XElement.Parse(
            """
            <QueryPlan>
              <ParameterList>
                <ColumnReference Column="@1" ParameterCompiledValue="(42)" />
              </ParameterList>
              <SeekPredicates>
                <SeekPredicateNew>
                  <SeekKeys>
                    <Prefix ScanType="EQ">
                      <RangeColumns>
                        <ColumnReference Database="[InternalsViewerDemo]" Schema="[dbo]"
                                         Table="[ClusteredTable]" Column="Id" />
                      </RangeColumns>
                      <RangeExpressions>
                        <ScalarOperator ScalarString="CONVERT_IMPLICIT(int,[@1],0)">
                          <Convert DataType="int" Style="0" Implicit="true">
                            <ScalarOperator>
                              <Identifier>
                                <ColumnReference Column="@1" />
                              </Identifier>
                            </ScalarOperator>
                          </Convert>
                        </ScalarOperator>
                      </RangeExpressions>
                    </Prefix>
                  </SeekKeys>
                </SeekPredicateNew>
              </SeekPredicates>
            </QueryPlan>
            """);

        var parameters = PlanParameters.Parse(xml);

        var seekPredicates = xml.Elements().First(e => e.Name.LocalName == "SeekPredicates");

        var parser = new SeekPredicateParser(resolveParameter: parameters.Resolve);

        var bounds = parser.ParseSeekPredicates(seekPredicates);

        var seek = Assert.Single(bounds);

        Assert.Equal(1, seek.CompareWidth);
        Assert.True(seek.IsStartInclusive);
        Assert.True(seek.IsEndInclusive);
        Assert.Equal(42, seek.StartValue.Values[0].Numeric);
        Assert.Equal(42, seek.EndValue.Values[0].Numeric);
        Assert.Equal("Id", seek.StartValue.Values[0].ColumnName);
    }
}
