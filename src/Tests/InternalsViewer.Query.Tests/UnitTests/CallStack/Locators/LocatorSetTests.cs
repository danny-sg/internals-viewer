using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.Tests.UnitTests.CallStack.Locators;

[Trait("Category", "Unit")]
public class LocatorSetTests
{
    private const ulong Page336 = 0x0000_0001_0000_0150;

    private const ulong Slot5 = 0x0005_0000_0000_0000;

    [Fact]
    public void One_Page_Is_Named()
    {
        var set = Set(Page(Page336), Page(Page336));

        Assert.Equal("(1:336)", set.Label);
    }

    [Fact]
    public void One_Row_Is_Named()
    {
        Assert.Equal("(1:336:5)", Set(Row(Slot5 | Page336)).Label);
    }

    [Fact]
    public void Several_Pages_And_Rows_Are_Counted()
    {
        var set = Set(Page(Page336), Page(Page336 + 1), Row(Slot5 | Page336));

        Assert.Equal("2 Pages, 1 Row", set.Label);
        Assert.Equal("(1:336), (1:337), (1:336:5)", set.Detail);
    }

    [Fact]
    public void A_Long_List_Is_Cut_Short()
    {
        var set = Set([.. Enumerable.Range(0, 12).Select(i => Page(Page336 + (ulong)i))]);

        Assert.Equal("12 Pages", set.Label);
        Assert.EndsWith("(1:345), etc.", set.Detail);
    }

    [Fact]
    public void A_Page_Search_Finds_Pages_And_The_Rows_On_Them()
    {
        var search = LocatorSearch.ForPage(new PageAddress(1, 336), null);

        Assert.True(Set(Page(Page336)).Contains(search));
        Assert.True(Set(Row(Slot5 | Page336)).Contains(search));
        Assert.False(Set(Page(Page336 + 1)).Contains(search));
    }

    [Fact]
    public void A_Row_Search_Finds_Only_That_Row()
    {
        var search = LocatorSearch.ForPage(new PageAddress(1, 336), 5);

        Assert.True(Set(Row(Slot5 | Page336)).Contains(search));
        Assert.False(Set(Row(0x0006_0000_0000_0000 | Page336)).Contains(search));
        Assert.False(Set(Page(Page336)).Contains(search));
    }

    [Fact]
    public void Padding_Above_A_Page_Is_Ignored()
    {
        var set = Set(Page(0xABCD_0000_0000_0000 | Page336));

        Assert.True(set.Contains(LocatorSearch.ForPage(new PageAddress(1, 336), null)));
        Assert.Equal("(1:336)", set.Label);
    }

    [Fact]
    public void Sets_Combine()
    {
        var set = Set(Page(Page336));

        set.Add(Set(Row(Slot5 | Page336)));

        Assert.Equal("1 Page, 1 Row", set.Label);
    }

    private static Locator Page(ulong value) => new(2, LocatorValueType.Page, value);

    private static Locator Row(ulong value) => new(1, LocatorValueType.Row, value);

    private static LocatorSet Set(params Locator[] locators)
    {
        var set = new LocatorSet();

        foreach (var locator in locators)
        {
            set.Add(locator);
        }

        return set;
    }
}
