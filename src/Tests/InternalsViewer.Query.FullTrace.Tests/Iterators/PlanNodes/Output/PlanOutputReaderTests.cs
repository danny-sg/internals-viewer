using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes.Output;

[Trait("Category", "Unit")]
public class PlanOutputReaderTests
{
    private const ulong PlanObject = 0x1F4_0000_7000;

    private const ulong Outputs = 0x1F4_0000_8000;

    private const ulong OutputColumns = 0x1F4_0000_8100;

    private const ulong IdColumn = 0x1F4_0000_8200;

    private const ulong NameColumn = 0x1F4_0000_8300;

    private const ulong Unknown = 0x1F4_0000_9000;

    private const ulong Redirect = 0x1F4_0000_8400;

    [Fact]
    public void A_Plan_Object_Lists_The_Output_Columns_It_Was_Compiled_With()
    {
        var memory = new FakeTraceReplay().Write(PlanObject + 0x30, Outputs)
                                          .Write(Outputs + 0x08, 2)
                                          .Write(Outputs + 0x10, OutputColumns)
                                          .Write(OutputColumns, IdColumn)
                                          .Write(OutputColumns + 0x08, NameColumn)
                                          .Write(IdColumn + 0x30, 0x138UL)
                                          .Write(IdColumn + 0x40, 0xA0004UL)
                                          .Write(IdColumn + 0x50, PlanObject)
                                          .Write(NameColumn + 0x30, 0x3A7UL)
                                          .Write(NameColumn + 0x40, 0x3400D008_00000064UL)
                                          .Write(NameColumn + 0x50, Unknown);

        var columns = PlanOutputReader.Read(memory, PlanObject, new HashSet<ulong> { PlanObject });

        Assert.Equal([new PlanOutputColumn(IdColumn, PlanObject, new SqlTypeInfo(0x38, 1, 4, 10, 0, 0)),
                      new PlanOutputColumn(NameColumn, 0, new SqlTypeInfo(0xA7, 3, 100, 0, 0, 0x3400D008))],
                     columns);
    }

    [Fact]
    public void A_Storage_Column_Reference_Is_Read_From_Its_Own_Layout()
    {
        var memory = new FakeTraceReplay().Write(PlanObject + 0x30, Outputs)
                                          .Write(Outputs + 0x08, 1)
                                          .Write(Outputs + 0x10, OutputColumns)
                                          .Write(OutputColumns, NameColumn)
                                          .Write(NameColumn + 0x30, PlanObject)
                                          .Write(NameColumn + 0x38, 0x3A7UL)
                                          .Write(NameColumn + 0x48, 0x3400D008_00000064UL);

        var columns = PlanOutputReader.Read(memory, PlanObject, new HashSet<ulong> { PlanObject });

        Assert.Equal([new PlanOutputColumn(NameColumn, PlanObject, new SqlTypeInfo(0xA7, 3, 100, 0, 0, 0x3400D008))], columns);
    }

    [Fact]
    public void A_Batch_Redirect_Takes_Its_Type_From_The_Column_It_Redirects_To()
    {
        var memory = new FakeTraceReplay().Write(PlanObject + 0x30, Outputs)
                                          .Write(Outputs + 0x08, 1)
                                          .Write(Outputs + 0x10, OutputColumns)
                                          .Write(OutputColumns, Redirect)
                                          .Write(Redirect + 0x30, IdColumn)
                                          .Write(Redirect + 0x38, PlanObject)
                                          .Write(IdColumn + 0x30, 0x138UL)
                                          .Write(IdColumn + 0x40, 0xA0004UL)
                                          .Write(IdColumn + 0x50, Unknown);

        var columns = PlanOutputReader.Read(memory, PlanObject, new HashSet<ulong> { PlanObject });

        Assert.Equal([new PlanOutputColumn(Redirect, PlanObject, new SqlTypeInfo(0x38, 1, 4, 10, 0, 0))], columns);
    }

    [Fact]
    public void A_Plan_Object_Without_An_Output_List_Has_No_Output_Columns()
        => Assert.Empty(PlanOutputReader.Read(new FakeTraceReplay(), PlanObject, new HashSet<ulong>()));
}
