using InternalsViewer.Query.FullTrace.Functions;


namespace InternalsViewer.Query.FullTrace.Tests.Functions;

[Trait("Category", "Unit")]
public class InstanceMethodCollectorTests
{
    [Fact]
    public void A_Member_Function_Takes_Its_Iterator_As_This()
    {
        Assert.Equal(0, InstanceMethodCollector.InstanceRegister("CQScanRangeNew::Open(unsigned __int64 *)",
                                                                 "?Open@CQScanRangeNew@@UEAAXPEA_K@Z"));
    }

    [Fact]
    public void A_Static_Function_Takes_Its_Iterator_When_The_First_Argument_Is_One()
    {
        Assert.Equal(0, InstanceMethodCollector.InstanceRegister(
            "CQScanNew::GetRowOrReQualifyHelper(CQScanNew *,unsigned __int64 *,unsigned __int64 *,bool)",
            "?GetRowOrReQualifyHelper@CQScanNew@@SAJPEAV1@PEA_K1_N@Z"));
    }

    [Fact]
    public void A_Static_Function_Without_An_Iterator_Argument_Is_Not_Tracked()
    {
        Assert.Null(InstanceMethodCollector.InstanceRegister("CQScanFoo::Open(int)", "?Open@CQScanFoo@@SAXH@Z"));
    }

    [Fact]
    public void A_Function_Without_A_Decorated_Name_Is_Tracked_By_Its_Name_Alone()
    {
        Assert.Equal(0, InstanceMethodCollector.InstanceRegister("CQScanFoo::Open(int)", null));
    }

    [Fact]
    public void A_Static_Function_Takes_The_First_Plan_Object_Among_Its_Arguments()
    {
        Assert.Equal(1, InstanceMethodCollector.InstanceRegister("CBpXte::CreateBatchIterators(CBpContext *,CXte *,CXte *,int)",
                                                                 "?CreateBatchIterators@CBpXte@@CAXPEAVCBpContext@@PEAVCXte@@1H@Z"));
    }

    [Fact]
    public void Every_Method_Of_A_Plan_Object_Is_Tracked()
    {
        Assert.True(InstanceMethodCollector.IsPlanObjectMethod("CXteRange::QScanGet"));
        Assert.True(InstanceMethodCollector.IsPlanObjectMethod("CBpXteHashAgg::PbpqsGet"));
        Assert.False(InstanceMethodCollector.IsPlanObjectMethod("CQScanRangeNew::Open"));
    }
}
