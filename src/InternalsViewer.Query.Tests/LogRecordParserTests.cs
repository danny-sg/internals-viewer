using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.TransactionLog;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Tests;

public class LogRecordParserTests
{
    private const string ModifyRowRecord =
        "00003E003300000028FC0100010002003E440000000004016001000001000000F50000003300000013FB0100020000010000" +
        "8E000000000117001700460006001700090000001400000000002E00546869732069732074686520666972737420726F7700" +
        "2000557064617465640000000101000C00007ED2CF4800000102000402030004";

    private const string CompensationModifyRowRecord =
        "00003E003300000028FC0100010003003E440000000004016001000001000000F50000003300000028FC0100020000010000" +
        "8E0000000001170009000D0006000000170000000000000000002E00546869732069732074686520666972737420726F7700";

    private const string BeginTransactionRecord =
        "00004C000000000000000000000002003E4400000000800057000000010000000100000000000000EDCBB9008CB400007D7B" +
        "02000000000000000000000000003E440000000000000000000002003E001C000000690076005F0051007500650072007900" +
        "5200650070006C00610079005F00310035003500380063003100660065003100300035003200340065003400660000000105" +
        "0000000000051500000054385A33A095BB2055BEA868EA030000";

    private const string AbortTransactionRecord =
        "000050003300000028FC0100010002003E44000000008200F9CBB9008CB400003300000028FC010001000100000000000000" +
        "00000000000000000000000000007D7B020000000000000000000000000000000000";

    private const string ModifyColumnsRecord =
        "00003E003400000040280000010002046246000000000601482800000100030007010000340000003F2800000200000100" +
        "009F000000000100000000460008000800040000001400020002000C000800080008001B001B0002000C000101000C0000" +
        "4A0CC67200000102000402030004E8033E00590400002700424242424244444444442300424242424278";

    private const string GamAllocationSetBitsRecord =
        "0000360034000000340E0000050002005345000000000708020000000100010003010000340000001" +
        "90E00002E000001290501000000010000000000";

    private const string GamDeallocationSetBitsRecord =
        "00003600340000000116000009000200B84500000000070802000000010001000301000034000000391300001800000129" +
        "050100010001000A0078780205000A4828000001007878";

    private const string SetFreeSpaceRecord =
        "000034000000000000000000000000000000000000000A0B981F0000010000006300000034000000340E00000A000000B4" +
        "084140";

    private const string HeapFormatPageRecord =
        "0000500034000000D916000008000200C945000000000101482800000100FFFF0501000034000000D91600000800000101" +
        "0000020000000000000000000000000000000000000000000000000000000000000000";

    private const string IamFormatPageRecord =
        "0000500034000000D91600000C000200C94500000000010ACA1700000100FFFF0501000034000000D91600000C0000010A" +
        "0000000000000000005A00000000000000000000000000010000000000000000000000";

    private const string CompactedHeapFormatPageRecord =
        "0000500034000000D916000016000200C945000000000101482800000100FFFF0501000034000000D91600001600000101" +
        "0000000000000000000800000000000000040000000000000000000000000000000000";

    private const string BeginCheckpointRecord =
        "0000600033000000C2FB010057000000000000000000960095CBB9008CB4000000000000000000000000E6033D4400000000" +
        "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

    [Fact]
    public void Parses_Header_Fields()
    {
        var lsn = LogSequenceNumberParser.Parse("00000033:0001FC28:0002");

        var record = LogRecordParser.Parse(lsn, Convert.FromHexString(ModifyRowRecord));

        Assert.Equal(lsn, record.Lsn);
        Assert.Equal(LogSequenceNumberParser.Parse("00000033:0001FC28:0001"), record.PreviousLsn);
        Assert.Equal("0000:0000443e", record.LogTransactionId);
        Assert.Equal(0x443E, record.TransactionId);
        Assert.Equal(LogOperation.LOP_MODIFY_ROW, record.Operation);
        Assert.Equal(LogContext.HEAP, record.Context);
        Assert.Equal(62, record.LogRecordSize);
    }

    [Fact]
    public void Parses_Page_Fields_From_Modify_Row_Record()
    {
        var record = Assert.IsType<ModifyRowLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(ModifyRowRecord)));

        Assert.Equal(new PageAddress(1, 352), record.PageAddress);
        Assert.Equal(0, record.SlotId);
        Assert.Equal(LogSequenceNumberParser.Parse("00000033:0001FB13:0002"), record.PreviousPageLsn);
        Assert.Equal(72057594047234048, record.PartitionId);
        Assert.Equal(6, record.ElementCount);
        Assert.Equal(23, record.OffsetInRow);
        Assert.Equal(23, record.ModifySize);
    }

    [Fact]
    public void Parses_Aligned_Before_And_After_Data_From_Modify_Row_Record()
    {
        var record = Assert.IsType<ModifyRowLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(ModifyRowRecord)));

        Assert.Equal(Convert.FromHexString("2E00546869732069732074686520666972737420726F77"), record.BeforeData);
        Assert.Equal(Convert.FromHexString("200055706461746564"), record.AfterData);
    }

    [Fact]
    public void Parses_Empty_Before_Data_From_Compensation_Record()
    {
        var record = Assert.IsType<ModifyRowLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(CompensationModifyRowRecord)));

        Assert.Empty(record.BeforeData);
        Assert.Equal(Convert.FromHexString("2E00546869732069732074686520666972737420726F77"), record.AfterData);
        Assert.Equal(6, record.ElementCount);
        Assert.Equal(23, record.OffsetInRow);
        Assert.Equal(9, record.ModifySize);
    }

    [Fact]
    public void Parses_Transaction_Details_From_Begin_Transaction_Record()
    {
        var record = Assert.IsType<BeginTransactionLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(BeginTransactionRecord)));

        Assert.Equal(LogOperation.LOP_BEGIN_XACT, record.Operation);
        Assert.Equal("iv_QueryReplay_1558c1fe10524e4f", record.TransactionName);
        Assert.Equal(Convert.FromHexString("01050000000000051500000054385A33A095BB2055BEA868EA030000"),
                     record.TransactionSid);
        Assert.Equal(new DateTime(2026, 7, 19, 11, 16, 27, 883), record.BeginTime, TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public void Parses_End_Time_From_Abort_Transaction_Record()
    {
        var record = Assert.IsType<EndTransactionLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(AbortTransactionRecord)));

        Assert.Equal(LogOperation.LOP_ABORT_XACT, record.Operation);
        Assert.Equal(new DateTime(2026, 7, 19, 11, 16, 27, 923), record.EndTime, TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public void Parses_Modification_Regions_From_Modify_Columns_Record()
    {
        var record = Assert.IsType<ModifyColumnsLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(ModifyColumnsRecord)));

        Assert.Equal(new PageAddress(1, 0x2848), record.PageAddress);
        Assert.Equal(3, record.SlotId);
        Assert.Equal(72057594048348160, record.PartitionId);
        Assert.Equal(8, record.ElementCount);

        Assert.Equal(2, record.Modifications.Count);

        Assert.Equal(8, record.Modifications[0].BeforeOffset);
        Assert.Equal(8, record.Modifications[0].AfterOffset);
        Assert.Equal(2, record.Modifications[0].BeforeLength);
        Assert.Equal(12, record.Modifications[1].BeforeLength);
        Assert.Equal(Convert.FromHexString("E803"), record.Modifications[0].BeforeData);
        Assert.Equal(Convert.FromHexString("5904"), record.Modifications[0].AfterData);

        Assert.Equal(27, record.Modifications[1].BeforeOffset);
        Assert.Equal(27, record.Modifications[1].AfterOffset);
        Assert.Equal(Convert.FromHexString("270042424242424444444444"), record.Modifications[1].BeforeData);
        Assert.Equal(Convert.FromHexString("2300424242424278"), record.Modifications[1].AfterData);
    }

    [Fact]
    public void Parses_Bit_Fields_From_Gam_Allocation_Set_Bits_Record()
    {
        var record = Assert.IsType<SetBitsLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(GamAllocationSetBitsRecord)));

        Assert.Equal(LogContext.GAM, record.Context);
        Assert.Equal(new PageAddress(1, 2), record.PageAddress);
        Assert.Equal(1, record.SlotId);
        Assert.Equal(1321, record.FirstBit);
        Assert.Equal(1, record.BitCount);
        Assert.Equal(0, record.BitValue);
    }

    [Fact]
    public void Parses_Bit_Value_From_Gam_Deallocation_Set_Bits_Record()
    {
        var record = Assert.IsType<SetBitsLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(GamDeallocationSetBitsRecord)));

        Assert.Equal(1321, record.FirstBit);
        Assert.Equal(1, record.BitCount);
        Assert.Equal(1, record.BitValue);
    }

    [Fact]
    public void Parses_Values_From_Set_Free_Space_Record()
    {
        var record = Assert.IsType<SetFreeSpaceLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(SetFreeSpaceRecord)));

        Assert.Equal(LogContext.PFS, record.Context);
        Assert.Equal(new PageAddress(1, 0x1F98), record.PageAddress);
        Assert.Equal(0, record.SlotId);
        Assert.Equal(2228, record.PageOffset);
        Assert.Equal(0x41, record.NewValue);
        Assert.Equal(0x40, record.OldValue);
    }

    [Fact]
    public void Parses_Page_Format_Fields_From_Heap_Format_Page_Record()
    {
        var record = Assert.IsType<FormatPageLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(HeapFormatPageRecord)));

        Assert.Equal(LogContext.HEAP, record.Context);
        Assert.Equal(new PageAddress(1, 0x2848), record.PageAddress);
        Assert.Equal(-1, record.SlotId);
        Assert.Equal(1, record.PageType);
        Assert.Equal(0, record.PageLevel);
        Assert.Equal(2, record.FormatOption);
        Assert.Equal(0, record.PageStatusFlags);
    }

    [Fact]
    public void Parses_Page_Type_From_Iam_Format_Page_Record()
    {
        var record = Assert.IsType<FormatPageLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(IamFormatPageRecord)));

        Assert.Equal(LogContext.IAM, record.Context);
        Assert.Equal(10, record.PageType);
        Assert.Equal(0, record.FormatOption);
    }

    [Fact]
    public void Parses_Page_Stat_From_Format_Page_Record()
    {
        var record = Assert.IsType<FormatPageLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(CompactedHeapFormatPageRecord)));

        Assert.Equal(4, record.PageStatusFlags);
        Assert.Equal(0, record.FormatOption);
    }

    private const string ModifyHeaderRecord =
        "00003E00000000000000000000000000000000000000050B981F00000100000063000000340000003648000009000000" +
        "00000000000000000200000000000200010001000000000001000000";

    [Fact]
    public void Parses_Modify_Header_Record()
    {
        var record = Assert.IsType<ModifyHeaderLogRecord>(
            LogRecordParser.Parse(default, Convert.FromHexString(ModifyHeaderRecord)));

        Assert.Equal(LogOperation.LOP_MODIFY_HEADER, record.Operation);
        Assert.Equal(LogContext.PFS, record.Context);
        Assert.Equal(new PageAddress(1, 0x1F98), record.PageAddress);
        Assert.Equal(2, record.HeaderOffset);
        Assert.Equal([0x00], record.BeforeData);
        Assert.Equal([0x01], record.AfterData);
    }

    [Fact]
    public void Parses_Checkpoint_Record_As_Base_Record()
    {
        var record = LogRecordParser.Parse(default, Convert.FromHexString(BeginCheckpointRecord));

        Assert.IsType<LogRecord>(record);
        Assert.Equal(LogOperation.LOP_BEGIN_CKPT, record.Operation);
        Assert.Equal(LogContext.NULL, record.Context);
        Assert.Equal("0000:00000000", record.LogTransactionId);
    }
}
