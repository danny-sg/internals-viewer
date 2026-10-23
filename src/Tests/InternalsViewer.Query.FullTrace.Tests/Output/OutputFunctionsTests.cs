using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Output;

[Trait("Category", "Unit")]
public class OutputFunctionsTests
{
    [Fact]
    public void Functions_Are_Typed_By_Their_Full_Name()
    {
        var collector = new OutputFunctions.Collector();

        collector.VisitFunction(0x10, "CEsExec::GeneralEval4");

        collector.VisitFunction(0x20, "GetDataFromXvar8");

        collector.VisitFunction(0x30, "CValOdsRow::SetDataX");

        collector.VisitFunction(0x40, "CEsRuntime::GeneralEval4");

        var functions = collector.Build(new Dictionary<ulong, string[]>()).Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, OutputFunctionType)[] expected =
        [
            (0x10, OutputFunctionType.Evaluation),
            (0x20, OutputFunctionType.VariantRead),
            (0x30, OutputFunctionType.Send)
        ];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void Value_Classes_Come_From_Their_Virtual_Function_Tables()
    {
        var vtables = new VtableResolver.Collector();

        vtables.VisitData(0x100, "CValHashSwitch::`vftable'");

        vtables.VisitData(0x200, "CValHashSwitch::`RTTI Complete Object Locator'");

        var classes = new OutputFunctions.Collector().Build(vtables.Build()).Classes;

        Assert.Equal(OutputValueClass.HashSwitch, Assert.Single(classes, c => c.Key == 0x100).Value);
        Assert.Single(classes);
    }
}
