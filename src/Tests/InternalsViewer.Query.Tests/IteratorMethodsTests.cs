using InternalsViewer.Query.CallStack.TimeTravel.Iterators;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class IteratorMethodsTests
{
    [Fact]
    public void A_Member_Function_Takes_Its_Iterator_As_This()
    {
        Assert.True(IteratorMethods.TakesIterator("CQScanRangeNew::Open(unsigned __int64 *)", "?Open@CQScanRangeNew@@UEAAXPEA_K@Z"));
    }

    [Fact]
    public void A_Static_Function_Takes_Its_Iterator_When_The_First_Argument_Is_One()
    {
        Assert.True(IteratorMethods.TakesIterator(
            "CQScanNew::GetRowOrReQualifyHelper(CQScanNew *,unsigned __int64 *,unsigned __int64 *,bool)",
            "?GetRowOrReQualifyHelper@CQScanNew@@SAJPEAV1@PEA_K1_N@Z"));
    }

    [Fact]
    public void A_Static_Function_Whose_First_Argument_Is_Not_An_Iterator_Is_Not_Tracked()
    {
        Assert.False(IteratorMethods.TakesIterator("CQScanFoo::Open(int)", "?Open@CQScanFoo@@SAXH@Z"));
    }

    [Fact]
    public void A_Function_Without_A_Decorated_Name_Is_Tracked_By_Its_Name_Alone()
    {
        Assert.True(IteratorMethods.TakesIterator("CQScanFoo::Open(int)", null));
    }
}
