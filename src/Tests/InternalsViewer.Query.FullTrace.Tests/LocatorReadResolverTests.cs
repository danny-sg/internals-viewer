using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class LocatorReadResolverTests
{
    private const ulong Function = 0x1000;

    [Fact]
    public void A_Page_Passed_By_Const_Reference_Is_Read_When_Called()
    {
        var reads = Reads("BPool::Get(RecoveryUnit &,PageId const &,int &,LatchBase::LATCH_TYPE,LatchSuspendInfo *,bool)",
                          "?Get@BPool@@QEAAPEAUBUF@@AEAVRecoveryUnit@@AEBVPageId@@AEAHW4LATCH_TYPE@LatchBase@@"
                          + "PEAVLatchSuspendInfo@@_N@Z");

        Assert.Equal([new LocatorRead(Function, 2, false, LocatorValueType.Page)], reads.Where(r => r.ValueType == LocatorValueType.Page));
    }

    [Fact]
    public void A_Value_Behind_A_Reference_Is_Read_When_Called_And_On_Return()
    {
        var reads = Reads("BPool::Get(RecoveryUnit &,PageId const &,int &,LatchBase::LATCH_TYPE,LatchSuspendInfo *,bool)",
                          "?Get@BPool@@QEAAPEAUBUF@@AEAVRecoveryUnit@@AEBVPageId@@AEAHW4LATCH_TYPE@LatchBase@@"
                          + "PEAVLatchSuspendInfo@@_N@Z");

        Assert.Equal([new LocatorRead(Function, 3, false, LocatorValueType.Referenced),
                      new LocatorRead(Function, 3, true, LocatorValueType.Referenced)],
                     reads.Where(r => r.ValueType == LocatorValueType.Referenced));
    }

    [Fact]
    public void A_Page_Handle_Is_Followed_To_The_Page_Header_When_Called_And_On_Return()
    {
        var reads = Reads("IndexPageManager::ReleaseHPage(HPageHandle &)", "?ReleaseHPage@IndexPageManager@@QEAAXAEAVHPageHandle@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorValueType.Page, 0x70, LocatorType.PageHandlePath),
                      new LocatorRead(Function, 1, true, LocatorValueType.Page, 0x70, LocatorType.PageHandlePath)],
                     reads);
    }

    [Fact]
    public void A_Page_Reference_Is_Followed_To_Its_Buffer_When_Called_And_On_Return()
    {
        var reads = Reads("SMOLockOrder::Add(PageRef &)", "?Add@SMOLockOrder@@QEAAXAEAVPageRef@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorValueType.Page, 0x70, LocatorType.PageRefPath),
                      new LocatorRead(Function, 1, true, LocatorValueType.Page, 0x70, LocatorType.PageRefPath)],
                     reads);
    }

    [Fact]
    public void A_BTree_Page_Is_Read_Like_The_Page_Reference_It_Starts_With()
    {
        var reads = Reads("BTreeMgr::HandleRoot(BTreeMgr::BTREE_TRAVERSAL_MODE,int,int &,PageRef &,BTreeHPage *)",
                          "?HandleRoot@BTreeMgr@@AEAA?AW4VR_STATUS@1@W4BTREE_TRAVERSAL_MODE@1@HAEAHAEAVPageRef@@PEAVBTreeHPage@@@Z");

        Assert.Equal([new LocatorRead(Function, 4, false, LocatorValueType.Page, 0x70, LocatorType.PageRefPath),
                      new LocatorRead(Function, 4, true, LocatorValueType.Page, 0x70, LocatorType.PageRefPath),
                      new LocatorRead(Function, 5, false, LocatorValueType.Page, 0x70, LocatorType.PageRefPath),
                      new LocatorRead(Function, 5, true, LocatorValueType.Page, 0x70, LocatorType.PageRefPath)],
                     reads.Where(r => r.ValueType == LocatorValueType.Page));
    }

    [Fact]
    public void A_Page_Filled_In_Through_A_Reference_Is_Read_On_Return()
    {
        var reads = Reads("IAM_BmapScan::GetNext(PageId &)", "?GetNext@IAM_BmapScan@@QEAAHAEAVPageId@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, true, LocatorValueType.Page)], reads);
    }

    [Fact]
    public void Pages_Passed_By_Value_Are_Read_When_Called()
    {
        var reads = Reads("FCB::CheckPageId(PageId,PageId)", "?CheckPageId@FCB@@QEAA_NVPageId@@0@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorValueType.Page), new LocatorRead(Function, 2, false, LocatorValueType.Page)],
                     reads);
    }

    [Fact]
    public void A_Row_Passed_By_Value_Is_Read_As_A_Row()
    {
        var reads = Reads("HeapDataSetSession::GetRowByRID(RowId,PageVersionId *,RowAccessOption,unsigned __int64 *)",
                          "?GetRowByRID@HeapDataSetSession@@AEAAXURowId@@PEAVPageVersionId@@W4RowAccessOption@@PEA_K@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorValueType.Row)], reads);
    }

    [Fact]
    public void A_Row_Filled_In_Through_A_Reference_Is_Read_On_Return()
    {
        var reads = Reads("HeapRowObject::GetRidAndPageVersionId(RowId &,PageVersionId *,CheckVisibilityOption)",
                          "?GetRidAndPageVersionId@HeapRowObject@@QEAAXAEAURowId@@PEAVPageVersionId@@W4CheckVisibilityOption@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, true, LocatorValueType.Row)], reads);
    }

    [Fact]
    public void A_Page_Returned_By_Value_Is_Read_Through_The_Return_Value()
    {
        var reads = Reads("BlobNode::ChildPID(int)", "?ChildPID@BlobNode@@QEBA?AVPageId@@H@Z");

        Assert.Equal([new LocatorRead(Function, ArgumentLayout.ReturnRegister, true, LocatorValueType.Page)], reads);
    }

    [Fact]
    public void A_Row_Returned_By_Pointer_Is_Read_Through_The_Return_Value()
    {
        var reads = Reads("Record::GetBackPtr(void)", "?GetBackPtr@Record@@QEBAPEFBURowId@@XZ");

        Assert.Equal([new LocatorRead(Function, ArgumentLayout.ReturnRegister, true, LocatorValueType.Row)], reads);
    }

    [Fact]
    public void An_Allocation_Page_Is_Read_From_The_Page_Inside_It()
    {
        var reads = Reads("UnOrderPageScanner::GetNextPage(int,AllocationPageId &)",
                          "?GetNextPage@UnOrderPageScanner@@UEAAXHAEAVAllocationPageId@@@Z");

        Assert.Equal([new LocatorRead(Function, 2, true, LocatorValueType.Page, 8)], reads);
    }

    [Fact]
    public void A_Columnstore_Object_Key_Is_Read_Through_Its_Reference()
    {
        var reads = Reads("ColumnStoreObjectRef::Fix(ColumnStoreObjectKey const &,bool,HoBtAccess *,XDES *,"
                          + "DeserializeFromDiskConstructor *)",
                          "?Fix@ColumnStoreObjectRef@@QEAAPEAVColumnStoreObject@@AEBVColumnStoreObjectKey@@_NPEAVHoBtAccess@@PEAVXDES@@"
                          + "PEAUDeserializeFromDiskConstructor@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorValueType.ColumnStoreObject)], reads);
    }

    [Fact]
    public void A_Parameter_On_The_Stack_Is_Read_From_Its_Slot()
    {
        var reads = Reads("IndexPageManager::GetPageWithKey(SEBindings const *,BTreeMgr::BTREE_SEEK_OPTION,AccessMode,HPageHandle &,"
                          + "SearchInfo &,SEBindings *,unsigned char *,unsigned long,HPageIdVersionId *,bool)",
                          "?GetPageWithKey@IndexPageManager@@QEAAXPEBVSEBindings@@W4BTREE_SEEK_OPTION@BTreeMgr@@W4AccessMode@@"
                          + "AEAVHPageHandle@@AEAVSearchInfo@@PEAV2@PEAEKPEAVHPageIdVersionId@@_N@Z");

        Assert.Equal([new LocatorRead(Function, 4, false, LocatorValueType.Page, 0x70, LocatorType.PageHandlePath),
                      new LocatorRead(Function, 4, true, LocatorValueType.Page, 0x70, LocatorType.PageHandlePath)],
                     reads);
    }

    [Fact]
    public void A_Log_Sequence_Number_Is_Read_When_Passed_And_On_Return_When_Filled_In()
    {
        var reads = Reads("WritePageRecorder2::HaveStaleLsnInternal(PageId const &,LSN const &,bool,bool,LSN &)",
                          "?HaveStaleLsnInternal@WritePageRecorder2@@AEAAHAEBVPageId@@AEBVLSN@@_N2AEAV3@@Z");

        Assert.Equal([new LocatorRead(Function, 2, false, LocatorValueType.LogSequenceNumber),
                      new LocatorRead(Function, 5, true, LocatorValueType.LogSequenceNumber)],
                     reads.Where(r => r.ValueType == LocatorValueType.LogSequenceNumber));
    }

    [Fact]
    public void An_Output_Variant_Is_Read_On_Return()
    {
        var reads = Reads("CEsExec::FastCompare1(unsigned __int64 *,CEsExec *,CXVariant *)",
                          "?FastCompare1@CEsExec@@CAXPEA_KPEAV1@PEAVCXVariant@@@Z");

        Assert.Equal([new LocatorRead(Function, 2, true, LocatorValueType.Variant)],
                     reads.Where(r => r.ValueType == LocatorValueType.Variant));
    }

    [Fact]
    public void A_Const_Variant_Is_Also_Read_On_Return()
    {
        var reads = Reads("CompareVariants(CXVariant const &,CXVariant const &)", "?CompareVariants@@YAHAEBVCXVariant@@0@Z");

        Assert.Equal([new LocatorRead(Function, 0, true, LocatorValueType.Variant),
                      new LocatorRead(Function, 1, true, LocatorValueType.Variant)],
                     reads);
    }

    [Fact]
    public void A_Type_Info_Is_Read_Through_Its_Reference()
    {
        var reads = Reads("ComputeConvertFlags(CTypeInfo const &,CTypeInfo const &)", "?ComputeConvertFlags@@YAKAEBVCTypeInfo@@0@Z");

        Assert.Equal([new LocatorRead(Function, 0, false, LocatorValueType.TypeInfo),
                      new LocatorRead(Function, 1, false, LocatorValueType.TypeInfo)],
                     reads);
    }

    [Fact]
    public void A_Character_Pointer_That_Can_Be_Written_To_Is_Not_Read_As_Text()
    {
        Assert.Null(LocatorType.GetLocatorType("char *"));
    }

    [Fact]
    public void The_Stack_Arguments_Of_A_Function_Are_Read_As_Values()
    {
        var signature = FunctionSignature.Parse("IndexPageManager::GetPageWithKey(SEBindings const *,BTreeMgr::BTREE_SEEK_OPTION,AccessMode,"
                                                + "HPageHandle &,SearchInfo &,SEBindings *,unsigned char *,unsigned long,"
                                                + "HPageIdVersionId *,bool)",
                                                "?GetPageWithKey@IndexPageManager@@QEAAXPEBVSEBindings@@W4BTREE_SEEK_OPTION@BTreeMgr@@"
                                                + "W4AccessMode@@AEAVHPageHandle@@AEAVSearchInfo@@PEAV2@PEAEKPEAVHPageIdVersionId@@_N@Z")!;

        var reads = LocatorReadResolver.GetStackReads(Function, signature).ToList();

        Assert.Equal([4, 5, 6, 7, 8, 9, 10], reads.Select(r => r.Register));
        Assert.All(reads, r => Assert.Equal((LocatorValueType.Argument, false), (r.ValueType, r.IsReadAtReturn)));
    }

    [Fact]
    public void A_Function_With_Four_Arguments_Has_Nothing_On_The_Stack()
    {
        var signature = new FunctionSignature(["int", "int", "int"], FunctionType.Member, "void");

        Assert.Empty(LocatorReadResolver.GetStackReads(Function, signature));
    }

    [Fact]
    public void A_Parameter_Past_The_Captured_Slots_Is_Not_Read()
    {
        var signature = new FunctionSignature([.. Enumerable.Repeat("int", 15), "PageId const &"], FunctionType.Member, "void");

        Assert.Empty(LocatorReadResolver.GetReads(Function, signature));
    }

    [Fact]
    public void A_Parameter_In_The_Last_Captured_Slot_Is_Read_From_The_Stack()
    {
        var signature = new FunctionSignature([.. Enumerable.Repeat("int", 14), "PageId const &"], FunctionType.Member, "void");

        Assert.Equal([new LocatorRead(Function, 15, false, LocatorValueType.Page)], LocatorReadResolver.GetReads(Function, signature));
    }

    [Theory]
    [InlineData("PageId const &", LocatorValueType.Page, true, true)]
    [InlineData("PageId", LocatorValueType.Page, false, false)]
    [InlineData("RowId &", LocatorValueType.Row, true, false)]
    [InlineData("RowId const __unaligned *", LocatorValueType.Row, true, true)]
    [InlineData("char const *", LocatorValueType.Text, true, true)]
    [InlineData("wchar_t const *", LocatorValueType.WideText, true, true)]
    [InlineData("ColumnStoreObjectKey const &", LocatorValueType.ColumnStoreObject, true, true)]
    [InlineData("AllocationPageId const *", LocatorValueType.Page, true, true, 8)]
    [InlineData("CTypeInfo const *", LocatorValueType.TypeInfo, true, true)]
    [InlineData("class CTypeInfo const &", LocatorValueType.TypeInfo, true, true)]
    [InlineData("CXVariant *", LocatorValueType.Variant, true, false)]
    [InlineData("tagDBBINDING const * const", LocatorValueType.Bindings, true, true)]
    [InlineData("class CXVariant const &", LocatorValueType.Variant, true, true)]
    public void A_Page_Or_Row_Type_Is_Recognised(string type, LocatorValueType valueType, bool indirect, bool isConst, int offset = 0)
        => Assert.Equal(new LocatorType(valueType, indirect, isConst, offset), LocatorType.GetLocatorType(type));

    [Theory]
    [InlineData("PageRef &")]
    [InlineData("BTreeHPage *")]
    [InlineData("IndexPageRef &")]
    [InlineData("HeapPageRef *")]
    [InlineData("TextPageRef &")]
    [InlineData("IAMPageRef *")]
    [InlineData("GAMPageRef *")]
    [InlineData("SGAMPageRef &")]
    [InlineData("PFSPageRef *")]
    [InlineData("PSPageRef &")]
    public void A_Page_Reference_Is_Read_Through_Its_Buffer(string type)
        => Assert.True(LocatorType.GetLocatorType(type) is { ValueType: LocatorValueType.Page, Offset: 0x70, IsIndirect: true, IsConst: false } found
                       && ReferenceEquals(found.Path, LocatorType.PageRefPath));

    [Theory]
    [InlineData("int &", true, false)]
    [InlineData("unsigned long const *", true, true)]
    [InlineData("enum LCK_MODE &", true, false)]
    [InlineData("enum BPool::StealOption *", true, false)]
    [InlineData("HoBtId const &", true, true)]
    public void A_Value_Behind_A_Reference_Is_Recognised(string type, bool indirect, bool isConst)
        => Assert.Equal(new LocatorType(LocatorValueType.Referenced, indirect, isConst), LocatorType.GetLocatorType(type));

    [Theory]
    [InlineData("AlignedPageId *")]
    [InlineData("FullRowId const &")]
    [InlineData("PageId * *")]
    [InlineData("PageIdOrderScan &")]
    [InlineData("unsigned __int64")]
    [InlineData("int")]
    [InlineData("char *")]
    [InlineData("unsigned char *")]
    [InlineData("enum UnknownOption &")]
    [InlineData("int * *")]
    public void Other_Types_Are_Not_Read(string type) => Assert.Null(LocatorType.GetLocatorType(type));

    [Fact]
    public void Folded_Functions_That_Agree_Keep_Their_Reads_And_Stack_Reads()
    {
        var collector = new LocatorReadResolver.Collector();

        const string signature = "BPool::Get(RecoveryUnit &,PageId const &,int &,LatchBase::LATCH_TYPE,LatchSuspendInfo *,bool)";

        const string decoratedName = "?Get@BPool@@QEAAPEAUBUF@@AEAVRecoveryUnit@@AEBVPageId@@AEAHW4LATCH_TYPE@LatchBase@@"
                                     + "PEAVLatchSuspendInfo@@_N@Z";

        collector.Visit(Symbol(signature, decoratedName, isFirst: true));

        collector.Visit(Symbol(signature, decoratedName, isFirst: false, hasSameName: false));

        var reads = collector.Build();

        Assert.Contains(new LocatorRead(Function, 2, false, LocatorValueType.Page), reads);
        Assert.Contains(new LocatorRead(Function, 5, false, LocatorValueType.Argument), reads);
    }

    [Fact]
    public void Folded_Functions_That_Disagree_On_Parameters_Read_Nothing()
    {
        var collector = new LocatorReadResolver.Collector();

        collector.Visit(Symbol("SMOLockOrder::Add(PageRef &)", "?Add@SMOLockOrder@@QEAAXAEAVPageRef@@@Z", isFirst: true));

        collector.Visit(Symbol("SMOLockOrder::Add(int)", "?Add@SMOLockOrder@@QEAAXH@Z", isFirst: false, hasSameParameters: false));

        Assert.Empty(collector.Build());
    }

    [Fact]
    public void A_Binding_Array_Takes_Its_Count_From_The_Argument_Before_It()
    {
        var signature = new FunctionSignature(["unsigned long",
                                               "unsigned __int64",
                                               "tagDBBINDING const * const",
                                               "unsigned __int64",
                                               "unsigned __int64 *",
                                               "unsigned long * const"],
                                              FunctionType.Member,
                                              "long");

        Assert.Equal(new LocatorRead(Function, 3, false, LocatorValueType.Bindings, CountRegister: 2), BindingRead(signature));
    }

    [Fact]
    public void A_Binding_Array_On_The_Stack_Takes_Its_Count_From_The_Stack()
    {
        var signature = new FunctionSignature(["XDES *",
                                               "HoBtAccess *",
                                               "unsigned long",
                                               "enum AccessorFlagEx",
                                               "unsigned __int64",
                                               "tagDBBINDING const * const"],
                                              FunctionType.Member,
                                              "AccBindings *");

        Assert.Equal(new LocatorRead(Function, 6, false, LocatorValueType.Bindings, CountRegister: 5), BindingRead(signature));
    }

    [Fact]
    public void A_Binding_Without_A_Count_Before_It_Is_One_Binding()
    {
        var signature = new FunctionSignature(["tagDBBINDING const *", "HoBtAccess const *"], FunctionType.Member, "void");

        Assert.Equal(new LocatorRead(Function, 1, false, LocatorValueType.Bindings), BindingRead(signature));
    }

    private static LocatorRead BindingRead(FunctionSignature signature)
        => Assert.Single(LocatorReadResolver.GetReads(Function, signature), r => r.ValueType == LocatorValueType.Bindings);

    private static List<LocatorRead> Reads(string signature, string decoratedName)
        => [.. LocatorReadResolver.GetReads(Function, FunctionSignature.Parse(signature, decoratedName)!)];

    private static FunctionSymbol Symbol(string signature,
                                        string decoratedName,
                                        bool isFirst,
                                        bool hasSameName = true,
                                        bool hasSameParameters = true)
        => new()
        {
            Rva = (uint)Function,
            Name = signature[..signature.IndexOf('(')],
            Signature = signature,
            DecoratedName = decoratedName,
            IsFirst = isFirst,
            HasSameName = hasSameName,
            HasSameParameters = hasSameParameters
        };
}
