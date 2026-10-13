using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class FunctionClassificationTests
{
    [Fact]
    public void Keeps_An_Event_Every_Symbol_At_The_Address_Agrees_On()
        => Assert.Equal("lock_acquired", Fold(Publishing("lock_acquired"), Publishing("lock_acquired")).PublishEventName);

    [Fact]
    public void Drops_An_Event_The_Symbols_Disagree_On()
        => Assert.Null(Fold(Publishing("lock_released"), Publishing("wait_info")).PublishEventName);

    [Fact]
    public void Drops_An_Event_Where_A_Folded_Symbol_Has_None()
        => Assert.Null(Fold(Publishing("lock_released"), Classification()).PublishEventName);

    [Fact]
    public void Keeps_A_Memory_Function_Every_Symbol_Agrees_On()
    {
        var function = MemoryFunction.Classify(0x10, "CMemObj::Alloc");

        var same = MemoryFunction.Classify(0x10, "CMemObj::Alloc");

        Assert.Equal(function, Fold(Classification(memory: function), Classification(memory: same)).Memory);

        Assert.Null(Fold(Classification(memory: function), Classification()).Memory);
    }

    [Fact]
    public void Excludes_Only_When_Every_Symbol_Is_Excluded()
    {
        Assert.True(Fold(Classification(excluded: true), Classification(excluded: true)).Excluded);

        Assert.False(Fold(Classification(excluded: true), Classification()).Excluded);
    }

    [Fact]
    public void An_Instance_Method_Needs_One_Method_And_Only_Instance_Classes()
    {
        Assert.True(Fold(Classification(method: true, instanceClass: true), Classification(instanceClass: true)).IsInstanceMethod);

        Assert.False(Fold(Classification(method: true, instanceClass: true), Classification()).IsInstanceMethod);

        Assert.False(Fold(Classification(instanceClass: true), Classification(instanceClass: true)).IsInstanceMethod);
    }

    [Fact]
    public void Folds_Only_The_Symbols_At_The_Same_Address()
    {
        var functions = new Dictionary<uint, FunctionClassification>();

        ModuleSymbols.Add(functions, 0x10, Publishing("latch_acquired"));

        ModuleSymbols.Add(functions, 0x20, Publishing("lock_released"));

        ModuleSymbols.Add(functions, 0x20, Classification());

        Assert.Equal("latch_acquired", functions[0x10].PublishEventName);

        Assert.Null(functions[0x20].PublishEventName);
    }

    private static FunctionClassification Fold(params FunctionClassification[] classifications)
    {
        var functions = new Dictionary<uint, FunctionClassification>();

        foreach (var classification in classifications)
        {
            ModuleSymbols.Add(functions, 0x10, classification);
        }

        return functions[0x10];
    }

    private static FunctionClassification Classification(bool excluded = false,
                                                         bool method = false,
                                                         bool instanceClass = false,
                                                         string? publishEventName = null,
                                                         MemoryFunction? memory = null)
        => new(excluded, method, instanceClass, IsEventWriter: false, publishEventName, memory);

    private static FunctionClassification Publishing(string eventName) => Classification(publishEventName: eventName);
}
