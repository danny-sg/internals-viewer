using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ArgumentValueTests
{
    [Theory]
    [InlineData("bool", 0x100ul, "false")]
    [InlineData("bool", 0x1ul, "true")]
    [InlineData("int", 0xFFFFFFFFul, "-1")]
    [InlineData("unsigned long", 0x1_0000_0005ul, "5")]
    [InlineData("unsigned __int64", 643ul, "643")]
    [InlineData("unsigned __int64", 0x40EC6ul, "265926 (0x40EC6)")]
    [InlineData("short", 0xFFFFul, "-1")]
    [InlineData("SCANLOC", 3ul, "0x3")]
    public void A_Value_Is_Formatted_For_Its_Type(string type, ulong raw, string expected)
    {
        Assert.Equal(expected, ArgumentValue.Format(type, raw));
    }

    [Fact]
    public void A_Pointer_Is_Shown_As_An_Address()
    {
        Assert.Equal("0x2E0114C5870", ArgumentValue.Format("unsigned __int64 *", 0x2E0114C5870));
        Assert.Equal("nullptr", ArgumentValue.Format("CQScanNew *", 0));
    }

    [Fact]
    public void A_Double_Is_Read_From_Its_Bits()
    {
        Assert.Equal("1.5", ArgumentValue.Format("double", (ulong)BitConverter.DoubleToInt64Bits(1.5)));
    }

    [Theory]
    [InlineData("unsigned __int64 *", "unsigned __int64")]
    [InlineData("int const *", "int")]
    [InlineData("bool &", "bool")]
    public void The_Pointee_Drops_The_Pointer_And_Qualifiers(string type, string expected)
    {
        Assert.Equal(expected, ArgumentValue.Pointee(type));

        Assert.True(ArgumentValue.IsPrimitivePointer(type));
    }

    [Fact]
    public void A_Pointer_To_A_Pointer_Is_Not_A_Primitive_Pointer()
    {
        Assert.False(ArgumentValue.IsPrimitivePointer("char * *"));
        Assert.False(ArgumentValue.IsPrimitivePointer("CQScanNew *"));
    }
}
