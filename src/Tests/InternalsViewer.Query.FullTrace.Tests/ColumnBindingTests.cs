using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class ColumnBindingTests
{
    [Theory]
    [InlineData(3, "I4")]
    [InlineData(130, "WSTR")]
    [InlineData(0x4082, "BYREF WSTR")]
    [InlineData(146, "Type 146")]
    public void A_Type_Is_Named_By_Its_OLE_DB_Type(int type, string expected)
    {
        Assert.Equal(expected, new ColumnBinding(1, (ushort)type, 0, 0, 0, 0).TypeName);
    }

    [Fact]
    public void Only_The_Bound_Parts_Show_Their_Offsets()
    {
        Assert.Equal("#4 I8 Status +0x20", new ColumnBinding(4, 20, 0x4, 0x10, 0x18, 0x20).ToString());
    }
}
