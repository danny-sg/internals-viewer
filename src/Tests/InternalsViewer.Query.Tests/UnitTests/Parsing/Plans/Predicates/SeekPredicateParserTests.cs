using System.Xml.Linq;
using InternalsViewer.Query.Parsing.Plans.Predicates;

namespace InternalsViewer.Query.Tests.UnitTests.Parsing.Plans.Predicates;

public class SeekPredicateParserTests
{
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
    }
}
