using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Locators.Parsers;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class LocatorParsersTests
{
    [Fact]
    public void Every_Value_Type_Read_From_Memory_Has_A_Parser()
    {
        var missing = Enum.GetValues<LocatorValueType>()
                          .Where(t => t != LocatorValueType.Argument && LocatorParsers.Default.Get(t) is null)
                          .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Each_Parser_Reads_Its_Own_Value_Type()
    {
        Assert.All(Enum.GetValues<LocatorValueType>().Where(t => t != LocatorValueType.Argument),
                   t => Assert.Equal(t, LocatorParsers.Default.Get(t)?.ValueType));
    }

    [Theory]
    [InlineData("CXVariant", LocatorValueType.Variant)]
    [InlineData("HPageHandle", LocatorValueType.Page)]
    [InlineData("RowId", LocatorValueType.Row)]
    [InlineData("wchar_t", LocatorValueType.WideText)]
    public void A_Type_Name_Finds_The_Parser_That_Declares_It(string name, LocatorValueType expected)
        => Assert.Equal(expected, LocatorParsers.Default.GetKnownType(name)?.Parser.ValueType);

    [Fact]
    public void Only_Variants_Are_Always_Read_At_Return()
        => Assert.Equal([LocatorValueType.Variant], LocatorParsers.Default.All.Where(p => p.IsReadAtReturn).Select(p => p.ValueType));
}
