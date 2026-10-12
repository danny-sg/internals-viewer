using InternalsViewer.Query.CallStack.TimeTravel.Iterators;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class IteratorMethodsTests
{
    [Fact]
    public void A_Member_Function_Takes_Its_Iterator_As_This()
    {
        Assert.Equal(0, IteratorMethods.InstanceRegister("CQScanRangeNew::Open(unsigned __int64 *)", "?Open@CQScanRangeNew@@UEAAXPEA_K@Z"));
    }

    [Fact]
    public void A_Static_Function_Takes_Its_Iterator_When_The_First_Argument_Is_One()
    {
        Assert.Equal(0, IteratorMethods.InstanceRegister(
            "CQScanNew::GetRowOrReQualifyHelper(CQScanNew *,unsigned __int64 *,unsigned __int64 *,bool)",
            "?GetRowOrReQualifyHelper@CQScanNew@@SAJPEAV1@PEA_K1_N@Z"));
    }

    [Fact]
    public void A_Static_Function_Without_An_Iterator_Argument_Is_Not_Tracked()
    {
        Assert.Null(IteratorMethods.InstanceRegister("CQScanFoo::Open(int)", "?Open@CQScanFoo@@SAXH@Z"));
    }

    [Fact]
    public void A_Function_Without_A_Decorated_Name_Is_Tracked_By_Its_Name_Alone()
    {
        Assert.Equal(0, IteratorMethods.InstanceRegister("CQScanFoo::Open(int)", null));
    }

    [Fact]
    public void A_Static_Function_Takes_The_First_Plan_Object_Among_Its_Arguments()
    {
        Assert.Equal(1, IteratorMethods.InstanceRegister("CBpXte::CreateBatchIterators(CBpContext *,CXte *,CXte *,int)",
                                                         "?CreateBatchIterators@CBpXte@@CAXPEAVCBpContext@@PEAVCXte@@1H@Z"));
    }

    [Fact]
    public void Every_Method_Of_A_Plan_Object_Is_Tracked()
    {
        Assert.True(IteratorMethods.IsPlanObjectMethod("CXteRange::QScanGet"));
        Assert.True(IteratorMethods.IsPlanObjectMethod("CBpXteHashAgg::PbpqsGet"));
        Assert.False(IteratorMethods.IsPlanObjectMethod("CQScanRangeNew::Open"));
    }
}
