using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class ArgumentValueTests
{
    [Theory]
    [InlineData("int", 5ul, "5")]
    [InlineData("unsigned __int64", 0x40EC6ul, "265926")]
    [InlineData("enum LCK_MODE", 21ul, "RX_X")]
    [InlineData("HoBtAccess *", 0x1_0000ul, null)]
    [InlineData("HoBtAccess *", 0ul, "nullptr")]
    [InlineData("this", 0x1_0000ul, null)]
    [InlineData("SCANLOC", 3ul, null)]
    public void A_Value_Decodes_To_Its_Reading_Alone(string type, ulong raw, string? expected)
    {
        Assert.Equal(expected, ArgumentValue.Decode(type, raw));
    }

    [Theory]
    [InlineData("bool", 0x100ul, "false")]
    [InlineData("bool", 0x1ul, "true")]
    [InlineData("int", 0xFFFFFFFFul, "-1")]
    [InlineData("unsigned long", 0x1_0000_0005ul, "5")]
    [InlineData("unsigned __int64", 643ul, "643")]
    [InlineData("unsigned __int64", 0x40EC6ul, "265926 (0x40EC6)")]
    [InlineData("short", 0xFFFFul, "-1")]
    [InlineData("SCANLOC", 3ul, "0x3")]
    [InlineData("enum CheckVisibilityOption", 0x1_0000_0002ul, "2")]
    [InlineData("enum LatchBase::LATCH_TYPE", 2ul, "SH")]
    [InlineData("enum LCK_MODE", 21ul, "RX_X")]
    [InlineData("enum LockRes::LockResourceType", 6ul, "Page")]
    [InlineData("enum PageHeader::PageType", 10ul, "Iam")]
    [InlineData("enum PageHeader::PageType", 6ul, "6")]
    [InlineData("enum RbpEncodingType", 8ul, "String Hash")]
    [InlineData("enum RbpFilterType", 20ul, "In List")]
    [InlineData("enum RbpCPUInstructionSet", 2ul, "Avx2")]
    [InlineData("enum MDObjectType", 0x2050ul, "PROC")]
    [InlineData("enum XeSqlPkg::FileIoMode", 1ul, "Scatter Gather")]
    [InlineData("enum ColumnStoreObjectType", 9ul, "Delete Bitmap Collection")]
    [InlineData("enum EDisable_Rowgroup_For_FPGA", 7ul, "EDELETE_BITMAP")]
    [InlineData("enum LCK_RESULT", 0xFFFFFFFFul, "TIMEDOUT")]
    [InlineData("enum LCK_RESULT", 0ul, "OK")]
    [InlineData("enum BTreeMgr::VR_STATUS", 1ul, "Root Leaf")]
    [InlineData("enum BPool::StealOption", 13ul, "Cold Buffer | For Short Term Use | No Zeroing")]
    [InlineData("enum BPool::StealOption", 0ul, "No Steal Option")]
    [InlineData("enum BPool::StealOption", 0x101ul, "Cold Buffer | 0x100")]
    [InlineData("enum SOS_Scheduler::Transition", 3ul, "Switch To Preemptive")]
    [InlineData("enum Worker::CallbackType", 1ul, "CALLBACK_RESUME")]
    [InlineData("enum PHYOP", 7ul, "Hash Match")]
    [InlineData("enum LOGOP", 2ul, "Concat")]
    [InlineData("enum QTETYPE", 2ul, "Executable")]
    [InlineData("enum PageReadError", 0xFFFFFFEFul, "BUFFER_MAX_ERROR")]
    [InlineData("enum EMDCompressLevel", 2ul, "Page")]
    [InlineData("enum EQDSResourceType", 1ul, "x_QdsResourceType_BufferedItemsMemory")]
    [InlineData("enum EQDSOperationalState", 2ul, "READ_WRITE")]
    [InlineData("enum EQDSExecType", 1ul, "Under Performance Affecting Conditions")]
    [InlineData("HoBtId", 0x1000000008Eul, "72057594047234048 (0x1000000008E0000)")]
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
    public void A_Value_Behind_A_Reference_Is_Formatted_For_The_Type_It_Refers_To()
    {
        Assert.Equal("U", ArgumentValue.FormatReferenced("enum LCK_MODE &", 4));
        Assert.Equal("-1", ArgumentValue.FormatReferenced("int *", 0xFFFFFFFF));
    }

    [Fact]
    public void A_HoBt_Id_Is_Only_Found_In_A_HoBtId()
    {
        Assert.Equal(72057594047234048ul, ArgumentValue.GetHoBtId("HoBtId", 0x1000000008E));
        Assert.Null(ArgumentValue.GetHoBtId("unsigned __int64", 0x1000000008E));
    }

    [Fact]
    public void A_Double_Is_Read_From_Its_Bits()
    {
        Assert.Equal("1.5", ArgumentValue.Format("double", (ulong)BitConverter.DoubleToInt64Bits(1.5)));
    }

    [Theory]
    [InlineData("enum BTreeMgr::SeekRetCode", "BTreeMgr::SeekRetCode")]
    [InlineData("enum const LatchBase::Releasor &", "LatchBase::Releasor")]
    [InlineData("enum CheckVisibilityOption *", "CheckVisibilityOption")]
    [InlineData("enum LatchBase::LATCH_TYPE", null)]
    [InlineData("enum const LatchBase::AcquireResult &", null)]
    [InlineData("enum std::align_val_t", null)]
    [InlineData("enum LCK_MODE &", null)]
    [InlineData("enum BPool::StealOption", null)]
    [InlineData("unsigned long", null)]
    [InlineData("PageId *", null)]
    public void An_Enum_Without_Known_Names_Is_Reported(string type, string? expected)
    {
        Assert.Equal(expected, ArgumentValue.GetUnknownEnum(type));
    }

    [Theory]
    [InlineData("LckInfo const *", "LckInfo")]
    [InlineData("class CMemObj &", "CMemObj")]
    [InlineData("CMemPartitioned<CMemThread<CMemObj> > *", "CMemPartitioned<CMemThread<CMemObj> >")]
    [InlineData("unsigned __int64 *", null)]
    [InlineData("char const *", null)]
    [InlineData("void *", null)]
    [InlineData("enum OperationType *", null)]
    [InlineData("LckInfo * *", null)]
    [InlineData("LckInfo", null)]
    public void A_Pointer_Names_The_Class_It_Points_To(string type, string? expected)
    {
        Assert.Equal(expected, ArgumentValue.GetPointeeClass(type));
    }
}
