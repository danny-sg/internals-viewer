using InternalsViewer.Query.FullTrace.RowBuffers.VariantRows;
using InternalsViewer.Query.FullTrace.RowBuffers.VariantRows.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class VariantRowFunctionsTests
{
    [Fact]
    public void The_Batch_Helper_And_The_Row_Mode_Helper_Return_Variant_Rows()
    {
        var collector = new VariantRowFunctions.Collector();

        collector.VisitFunction(0xA0, "CQScanBatchHelper::GetRow");
        collector.VisitFunction(0xB0, "CQScanNew::GetRowOrReQualifyHelper");
        collector.VisitFunction(0xC0, "CQScanSortNew::GetRow");

        var functions = collector.Build().Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, VariantRowFunctionType)[] expected =
        [
            (0xA0, VariantRowFunctionType.BatchHelper),
            (0xB0, VariantRowFunctionType.Iterator)
        ];

        Assert.Equal(expected, functions);
    }
}
