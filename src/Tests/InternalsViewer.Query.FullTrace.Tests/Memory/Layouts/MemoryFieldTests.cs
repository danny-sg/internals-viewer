using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts;

[Trait("Category", "Unit")]
public class MemoryFieldTests
{
    private static MemoryField Field { get; } =
        new("Values", 0x58, sizeof(ulong)) { Writers = ["CQScanHash::CreateInputAndOutput", "CXte*"] };

    [Theory]
    [InlineData("CQScanHash::CreateInputAndOutput", true)]
    [InlineData("CXteRange::QScanGetHelper", true)]
    [InlineData("CQScanHash::CQScanHash", false)]
    [InlineData("CBpXte::BpCompile", false)]
    public void A_Writer_Matches_By_Name_Or_By_Prefix(string writer, bool isExpected)
        => Assert.Equal(isExpected, Field.IsWrittenBy(writer));

    [Fact]
    public void A_Number_The_Size_Of_A_Pointer_Is_Not_A_Pointer()
        => Assert.False(new MemoryField("Length", 0x10, sizeof(ulong)) { IsNumber = true }.IsPointer);

    [Fact]
    public void A_Field_With_No_Writers_Expects_None()
        => Assert.False(new MemoryField("Count", 0x08, sizeof(int)).IsWrittenBy("CQScanHash::CQScanHash"));
}
