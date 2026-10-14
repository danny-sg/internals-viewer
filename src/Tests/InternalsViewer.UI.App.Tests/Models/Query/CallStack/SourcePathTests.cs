using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class SourcePathTests
{
    private const ulong Address = 0x21CC530AFC0;

    [Fact]
    public void Callers_That_Received_The_Value_Lead_Back_To_Its_Constructor()
    {
        var tree = new CallStackTree();

        var session = tree.AddCall(tree.Root, Frame("IndexDataSetSession", "GetRowByKeyValue", 0x10), 1);
        var constructor = tree.AddCall(session, Frame("SEBindings", "SEBindings", 0x20), 1);
        var manager = tree.AddCall(session, Frame("IndexPageManager", "GetPageWithKey", 0x30), 1);
        var seek = tree.AddCall(manager, Frame("BTreeMgr", "Seek", 0x40), 1);

        var origin = Origin(MemoryMapOperation.Constructed, 15);

        var value = Value(origin);

        var steps = SourcePath.Build(value,
                                     Row(seek, 40, value),
                                     [Row(manager, 30, Value()), Row(session, 20, Value()), Row(tree.Root, 0)],
                                     constructor);

        Assert.Equal([30ul, 20, 15], steps.Select(s => s.Id));
        Assert.Equal([0, 1, 2], steps.Select(s => s.Depth));
        Assert.Equal([null, null, MemoryMapOperation.Constructed], steps.Select(s => s.Creation));
        Assert.Equal("SEBindings::SEBindings", steps[^1].Node.Symbol);
    }

    [Fact]
    public void A_Caller_That_Did_Not_Receive_The_Value_Is_Skipped()
    {
        var tree = new CallStackTree();

        var session = tree.AddCall(tree.Root, Frame("IndexDataSetSession", "GetRowByKeyValue", 0x10), 1);
        var helper = tree.AddCall(session, Frame("IndexPageManager", "Helper", 0x20), 1);
        var seek = tree.AddCall(helper, Frame("BTreeMgr", "Seek", 0x30), 1);

        var value = Value(Origin(MemoryMapOperation.Constructed, 15));

        var steps = SourcePath.Build(value,
                                     Row(seek, 40, value),
                                     [Row(helper, 30, Value(other: true)), Row(session, 20, Value())],
                                     null);

        var step = Assert.Single(steps);

        Assert.Equal(20ul, step.Id);
        Assert.Equal(0, step.Depth);
    }

    [Fact]
    public void A_Caller_That_Started_Before_The_Value_Was_Created_Is_Not_A_Step()
    {
        var tree = new CallStackTree();

        var outer = tree.AddCall(tree.Root, Frame("Outer", "Run", 0x10), 1);
        var constructor = tree.AddCall(outer, Frame("SEBindings", "SEBindings", 0x20), 1);
        var seek = tree.AddCall(outer, Frame("BTreeMgr", "Seek", 0x30), 1);

        var value = Value(Origin(MemoryMapOperation.Constructed, 15));

        var steps = SourcePath.Build(value, Row(seek, 40, value), [Row(outer, 10, Value())], constructor);

        Assert.Equal([15ul], steps.Select(s => s.Id));
    }

    [Fact]
    public void A_Constructor_On_The_Stack_Ends_The_Path_There()
    {
        var tree = new CallStackTree();

        var outer = tree.AddCall(tree.Root, Frame("Outer", "Run", 0x10), 1);
        var constructor = tree.AddCall(outer, Frame("SEBindings", "SEBindings", 0x20), 1);
        var init = tree.AddCall(constructor, Frame("SEBindings", "Init", 0x30), 1);

        var value = Value(Origin(MemoryMapOperation.Constructed, 15));

        var steps = SourcePath.Build(value, Row(init, 40, value), [Row(constructor, 15, This()), Row(outer, 10)], constructor);

        Assert.Equal([15ul], steps.Select(s => s.Id));
        Assert.Equal(MemoryMapOperation.Constructed, steps[^1].Creation);
    }

    [Fact]
    public void A_Value_Selected_In_Its_Constructor_Has_No_Path()
    {
        var tree = new CallStackTree();

        var constructor = tree.AddCall(tree.Root, Frame("SEBindings", "SEBindings", 0x20), 1);

        var value = Value(Origin(MemoryMapOperation.Constructed, 15));

        var steps = SourcePath.Build(value, Row(constructor, 15, value), [], constructor);

        Assert.Empty(steps);
    }

    [Fact]
    public void A_First_Sighting_Ends_The_Path_Without_A_Creation()
    {
        var tree = new CallStackTree();

        var first = tree.AddCall(tree.Root, Frame("SEBindings", "Bind", 0x20), 1);
        var seek = tree.AddCall(tree.Root, Frame("BTreeMgr", "Seek", 0x30), 1);

        var value = Value(Origin(MemoryMapOperation.Visited, 12));

        var steps = SourcePath.Build(value, Row(seek, 40, value), [], first);

        var step = Assert.Single(steps);

        Assert.Equal(12ul, step.Id);
        Assert.Null(step.Creation);
    }

    [Fact]
    public void A_First_Sighting_After_The_Call_Is_Not_A_Step()
    {
        var tree = new CallStackTree();

        var later = tree.AddCall(tree.Root, Frame("SEBindings", "Bind", 0x20), 1);
        var seek = tree.AddCall(tree.Root, Frame("BTreeMgr", "Seek", 0x30), 1);

        var value = Value(Origin(MemoryMapOperation.Visited, 50));

        var steps = SourcePath.Build(value, Row(seek, 40, value), [], later);

        Assert.Empty(steps);
    }

    [Fact]
    public void A_Value_No_Caller_Received_And_With_No_Origin_Has_No_Path()
    {
        var tree = new CallStackTree();

        var manager = tree.AddCall(tree.Root, Frame("IndexPageManager", "GetPageWithKey", 0x20), 1);
        var seek = tree.AddCall(manager, Frame("BTreeMgr", "Seek", 0x30), 1);

        var value = Value();

        var steps = SourcePath.Build(value, Row(seek, 40, value), [Row(manager, 30, Value(other: true))], null);

        Assert.Empty(steps);
    }

    [Fact]
    public void A_Caller_Without_Parameter_Types_Is_Matched_On_Its_Registers()
    {
        var tree = new CallStackTree();

        var completion = tree.AddCall(tree.Root, Frame("", "JoinedIoCompletion", 0x10), 1);
        var read = tree.AddCall(completion, Frame("BPool", "ReadPageCompletion", 0x20), 1);

        var value = Value();

        var raw = new CallSignaturePart($"0x{Address:X}", CallSignaturePartKind.Value, Address, "RCX", true, location: "RCX");

        var steps = SourcePath.Build(value, Row(read, 40, value), [Row(completion, 30, raw)], null);

        Assert.Equal([30ul], steps.Select(s => s.Id));
    }

    [Fact]
    public void A_Step_On_The_Current_Stack_Does_Not_Expand()
    {
        var tree = new CallStackTree();

        var session = tree.AddCall(tree.Root, Frame("IndexDataSetSession", "GetRowByKeyValue", 0x10), 1);
        var manager = tree.AddCall(session, Frame("IndexPageManager", "GetPageWithKey", 0x20), 1);
        var seek = tree.AddCall(manager, Frame("BTreeMgr", "Seek", 0x30), 1);

        var value = Value();

        var steps = SourcePath.Build(value, Row(seek, 40, value), [Row(manager, 30, Value())], null);

        var step = Assert.Single(steps);

        Assert.True(step.IsOnStack);
        Assert.False(step.CanExpand);

        step.Toggle();

        Assert.Empty(step.VisibleStack);
    }

    [Fact]
    public void An_Origin_Off_The_Current_Stack_Lists_Its_Callers()
    {
        var tree = new CallStackTree();

        var session = tree.AddCall(tree.Root, Frame("IndexDataSetSession", "GetRowByKeyValue", 0x10), 1);
        var constructor = tree.AddCall(session, Frame("SEBindings", "SEBindings", 0x20), 1);
        var seek = tree.AddCall(session, Frame("BTreeMgr", "Seek", 0x30), 1);

        var value = Value(Origin(MemoryMapOperation.Constructed, 15));

        var steps = SourcePath.Build(value, Row(seek, 40, value), [Row(session, 10)], constructor, 3);

        var step = Assert.Single(steps);

        Assert.Equal(3, step.Call);
        Assert.True(step.CanExpand);
        Assert.Equal(["IndexDataSetSession::GetRowByKeyValue"], step.Stack.Select(f => f.Symbol));
        Assert.All(step.Stack, f => Assert.Equal(15ul, f.Moment));
        Assert.Empty(step.VisibleStack);

        step.Toggle();

        Assert.Equal(step.Stack, step.VisibleStack);
    }

    private static MemoryMapEntry Origin(MemoryMapOperation operation, ulong call)
        => new(Address, operation, call, 0, 0, 1, 0, "SEBindings", null);

    private static CallSignaturePart Value(MemoryMapEntry? origin = null, bool other = false)
        => new($"0x{Address:X}",
               CallSignaturePartKind.Value,
               other ? Address + 8 : Address,
               canFind: true,
               label: "1",
               declaredType: "SEBindings const *",
               origin: origin);

    private static CallSignaturePart This() => new($"0x{Address:X}", CallSignaturePartKind.Value, Address, canFind: true, label: "this");

    private static PlayheadCallRow Row(CallStackNode node, ulong id, CallSignaturePart? value = null)
    {
        List<CallSignaturePart> parts = [new(node.Symbol, CallSignaturePartKind.Function)];

        CallSignaturePart? self = null;

        if (value?.Label == "this")
        {
            self = value;
        }
        else if (value is not null)
        {
            parts.Add(value);
        }

        var call = new TraceStackCall(new TraceSpan(0, 1, 0, 0, default), 0, 0, 0);

        return new PlayheadCallRow(node, call, new CallSignature(parts, self, null), id, false);
    }

    private static CallstackFrame Frame(string className, string methodName, uint rva)
        => new() { Module = "sqlmin", Rva = rva, Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = methodName } };
}
