using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests.Functions;

[Trait("Category", "Unit")]
public class ReplayFunctionScanTests
{
    private const uint Function = 0x1000;

    private const string Module = "sqlmin";

    [Fact]
    public void Folded_Functions_That_Agree_Keep_Their_Reads_And_Stack_Reads()
    {
        const string signature = "BPool::Get(RecoveryUnit &,PageId const &,int &,LatchBase::LATCH_TYPE,LatchSuspendInfo *,bool)";

        const string decoratedName = "?Get@BPool@@QEAAPEAUBUF@@AEAVRecoveryUnit@@AEBVPageId@@AEAHW4LATCH_TYPE@LatchBase@@"
                                     + "PEAVLatchSuspendInfo@@_N@Z";

        var functions = Scan((signature, decoratedName),
                             (signature.Replace("BPool::", "BPoolMirror::"), decoratedName.Replace("@BPool@@", "@BPoolMirror@@")));

        Assert.Contains(new LocatorRead(Function, 2, false, LocatorValueType.Page), functions.LocatorReads);
        Assert.Equal(3, functions.StackArguments[Function]);
    }

    [Fact]
    public void Folded_Functions_That_Disagree_On_Parameters_Read_Nothing()
    {
        var functions = Scan(("SMOLockOrder::Add(PageRef &)", "?Add@SMOLockOrder@@QEAAXAEAVPageRef@@@Z"),
                             ("SMOLockOrder::Add(int)", "?Add@SMOLockOrder@@QEAAXH@Z"));

        Assert.Empty(functions.LocatorReads);
    }

    [Fact]
    public void An_Iterator_Method_Is_An_Instance_Method_Keyed_By_This()
    {
        var functions = Scan(("CQScanRangeNew::Open(unsigned __int64 *)", "?Open@CQScanRangeNew@@UEAAXPEA_K@Z"));

        Assert.Equal([new InstanceMethod(Function, 0)], functions.InstanceMethods);
    }

    private static ReplayFunctionTable Scan(params (string Signature, string DecoratedName)[] symbols)
    {
        var builder = new SymbolTable.Builder();

        foreach (var (signature, decoratedName) in symbols)
        {
            builder.Add(Function, true, signature[..signature.IndexOf('(')], signature, decoratedName);
        }

        return ReplayFunctionScan.Scan(builder.Build(), Module);
    }
}
