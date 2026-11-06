using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;

namespace InternalsViewer.Query.FullTrace.Tests.Functions;

[Trait("Category", "Unit")]
public class FunctionClassificationTests
{
    [Fact]
    public void Keeps_A_Memory_Function_Every_Symbol_Agrees_On()
    {
        var function = AllocationFunction.Classify(0x10, "CMemObj::Alloc");

        var same = AllocationFunction.Classify(0x10, "CMemObj::Alloc");

        Assert.Equal(function, Fold(Classification(memory: function), Classification(memory: same)).AllocationFunction);

        Assert.Null(Fold(Classification(memory: function), Classification()).AllocationFunction);
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

        FunctionClassification.Add(functions, 0x10, Classification(excluded: true));

        FunctionClassification.Add(functions, 0x20, Classification(excluded: true));

        FunctionClassification.Add(functions, 0x20, Classification());

        Assert.True(functions[0x10].IsExcluded);

        Assert.False(functions[0x20].IsExcluded);
    }

    [Fact]
    public void A_Plan_Node_Function_Folded_With_Any_Other_Function_Is_Not_One()
    {
        var iterator = Classification() with { PlanNodeFunction = PlanNodeFunctionType.Iterator };

        var constructor = Classification() with { PlanNodeFunction = PlanNodeFunctionType.Constructor };

        Assert.Equal(PlanNodeFunctionType.Iterator, Fold(iterator, constructor).PlanNodeFunction);

        Assert.Null(Fold(iterator, Classification(), constructor).PlanNodeFunction);
    }

    [Fact]
    public void The_First_Output_And_Buffer_Pool_Function_At_An_Address_Is_Kept()
    {
        var send = Classification() with { OutputFunction = OutputFunctionType.Send, BufferPoolFunction = BufferPoolFunctionType.Get };

        var read = Classification() with { OutputFunction = OutputFunctionType.ValueRead };

        var folded = Fold(Classification(), send, read);

        Assert.Equal((OutputFunctionType.Send, BufferPoolFunctionType.Get), (folded.OutputFunction, folded.BufferPoolFunction));
    }

    private static FunctionClassification Fold(params FunctionClassification[] classifications)
    {
        var functions = new Dictionary<uint, FunctionClassification>();

        foreach (var classification in classifications)
        {
            FunctionClassification.Add(functions, 0x10, classification);
        }

        return functions[0x10];
    }

    private static FunctionClassification Classification(bool excluded = false,
                                                         bool method = false,
                                                         bool instanceClass = false,
                                                         AllocationFunction? memory = null)
        => new(excluded, method, instanceClass, memory);
}
