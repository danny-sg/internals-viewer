using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class FunctionClassificationTests
{
    [Fact]
    public void Keeps_A_Memory_Function_Every_Symbol_Agrees_On()
    {
        var function = MemoryFunction.Classify(0x10, "CMemObj::Alloc");

        var same = MemoryFunction.Classify(0x10, "CMemObj::Alloc");

        Assert.Equal(function, Fold(Classification(memory: function), Classification(memory: same)).MemoryFunction);

        Assert.Null(Fold(Classification(memory: function), Classification()).MemoryFunction);
    }

    [Fact]
    public void Excludes_Only_When_Every_Symbol_Is_Excluded()
    {
        Assert.True(Fold(Classification(excluded: true), Classification(excluded: true)).IsExcluded);

        Assert.False(Fold(Classification(excluded: true), Classification()).IsExcluded);
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

        ModuleSymbols.Add(functions, 0x10, Classification(excluded: true));

        ModuleSymbols.Add(functions, 0x20, Classification(excluded: true));

        ModuleSymbols.Add(functions, 0x20, Classification());

        Assert.True(functions[0x10].IsExcluded);

        Assert.False(functions[0x20].IsExcluded);
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
                                                         MemoryFunction? memory = null)
        => new(excluded, method, instanceClass, memory);
}
