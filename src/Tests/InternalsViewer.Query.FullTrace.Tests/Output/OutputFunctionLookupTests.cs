using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Output;

[Trait("Category", "Unit")]
public class OutputFunctionLookupTests
{
    [Fact]
    public void Functions_Are_Typed_By_Their_Full_Name()
    {
        string[] names = ["CEsExec::GeneralEval4", "GetDataFromXvar8", "CValOdsRow::SetDataX", "CEsRuntime::GeneralEval4"];

        Assert.Equal([OutputFunctionType.Evaluation, OutputFunctionType.VariantRead, OutputFunctionType.Send, null],
                     names.Select(n => OutputFunctionLookup.GetFunctionType(n)));
    }

    [Fact]
    public void Value_Classes_Come_From_Their_Virtual_Function_Tables()
    {
        var vtables = new VtableResolver.Collector();

        vtables.VisitData(0x100, "CValHashSwitch::`vftable'");

        vtables.VisitData(0x200, "CValHashSwitch::`RTTI Complete Object Locator'");

        var classes = OutputFunctionLookup.GetValueClasses(vtables.Build());

        Assert.Equal(OutputValueClass.HashSwitch, Assert.Single(classes, c => c.Key == 0x100).Value);
        Assert.Single(classes);
    }
}
