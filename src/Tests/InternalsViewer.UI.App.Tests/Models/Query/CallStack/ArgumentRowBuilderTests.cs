using System.Collections;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class ArgumentRowBuilderTests
{
    private static readonly ArgumentLayout SetRange =
        ArgumentLayout.For(new FunctionSignature(["unsigned __int64 *"], FunctionKind.Member, "void"));

    [Fact]
    public void A_Call_Lists_Each_Argument_With_Its_Value()
    {
        var rows = ArgumentRowBuilder.ForCall(SetRange, Call(0x100, 0x200, 5, 6));

        Assert.Equal(["this", "Argument 1", "*Argument 1", "*Argument 1"], rows.Select(r => r.Name));
        Assert.Equal(["0x100", "0x200", "5", "6"], rows.Select(r => r.Value));
        Assert.Equal(["RCX", "RDX", "Entry", "Return"], rows.Select(r => r.Source));
    }

    [Fact]
    public void A_Null_Pointer_Has_No_Pointee()
    {
        var rows = ArgumentRowBuilder.ForCall(SetRange, Call(0x100, 0, null, null));

        Assert.Equal("Null Pointer", rows.Single(r => r.Source == "Entry").Value);
    }

    [Fact]
    public void A_Pointee_That_Was_Not_Recorded_Is_Unreadable()
    {
        var rows = ArgumentRowBuilder.ForCall(SetRange, Call(0x100, 0x200, null, null));

        Assert.Equal("Unreadable", rows.Single(r => r.Source == "Entry").Value);
    }

    [Fact]
    public void A_Void_Function_Has_No_Return()
    {
        var rows = ArgumentRowBuilder.ForCall(SetRange, Call(0x100, 0x200, 5, 6));

        Assert.DoesNotContain(rows, r => r.Name == "Return");
    }

    [Fact]
    public void A_Call_That_Did_Not_Return_Says_So()
    {
        var layout = ArgumentLayout.For(new FunctionSignature([], FunctionKind.Free, "int"));

        var row = Assert.Single(ArgumentRowBuilder.ForCall(layout, Call(0, 0, null, null, returned: false)));

        Assert.Equal(["Return", "int", "RAX", "Did Not Return"], [row.Name, row.Type, row.Source, row.Value]);
    }

    [Fact]
    public void Each_Call_Is_A_Row_With_A_Column_Per_Value()
    {
        List<TimeTravelArgumentCall> calls = [Call(0x100, 0x200, 5, 6)];

        var table = ArgumentRowBuilder.Table(SetRange, calls);

        Assert.Equal(["#", "Thread", "this", "Argument 1", "*Argument 1", "*Argument 1"], table.Columns.Select(c => c.Name));

        var row = Assert.Single(table.Rows);

        Assert.Equal([1L, 1u, "0x100", "0x200", "5", "6"], Enumerable.Range(0, row.FieldCount).Select(i => row[i]));
    }

    [Fact]
    public void Argument_Columns_Carry_Their_Type_And_Where_They_Are_Read()
    {
        var table = ArgumentRowBuilder.Table(SetRange, [Call(0x100, 0x200, 5, 6)]);

        var argument = table.Columns[3];

        Assert.Equal(("unsigned __int64 *", "RDX"), (argument.TypeName, argument.Detail));
        Assert.Equal(("unsigned __int64", "Return"), (table.Columns[5].TypeName, table.Columns[5].Detail));
    }

    [Fact]
    public void Values_At_Return_Are_Tinted()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["unsigned __int64 *"], FunctionKind.Member, "int"));

        var table = ArgumentRowBuilder.Table(layout, [Call(0x100, 0x200, 5, 6, returnValue: 3)]);

        Assert.Equal(["*Argument 1 Return", "Return RAX"],
                     table.Columns.Where(c => c.BackgroundColour is not null).Select(c => $"{c.Name} {c.Detail}"));
    }

    [Fact]
    public void Every_Call_Becomes_A_Row()
    {
        List<TimeTravelArgumentCall> calls = [.. Enumerable.Range(1, 5).Select(i => Call((ulong)i, 0, null, null))];

        var table = ArgumentRowBuilder.Table(SetRange, calls);

        Assert.Equal([1L, 2L, 3L, 4L, 5L], table.Rows.Select(r => r[0]));
        Assert.Equal([0L, 1L, 2L, 3L, 4L], table.Rows.Select(r => r.Id));
    }

    [Fact]
    public void A_Call_Is_Only_Read_When_Its_Row_Is_Shown()
    {
        var calls = new CountingCalls(10_000);

        var table = ArgumentRowBuilder.Table(SetRange, calls);

        Assert.Equal(10_000, table.Rows.Count);
        Assert.Equal(0, calls.Reads);

        Assert.Equal("0x1388", table.Rows[5_000][2]);
        Assert.Equal("nullptr", table.Rows[5_000][3]);

        Assert.Equal(1, calls.Reads);
    }

    [Fact]
    public void A_Value_That_Is_An_Iterator_Links_To_It()
    {
        var tree = new CallStackTree();

        var node = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10, Instance = 0x100 }, 1);

        var iterators = new Dictionary<ulong, IteratorTarget> { [0x100] = new(node, null) };

        var rows = ArgumentRowBuilder.ForCall(SetRange, Call(0x100, 0x200, 5, 6), iterators);

        Assert.Same(node, rows[0].Iterator?.Node);
        Assert.Null(rows[1].Iterator);
    }

    [Fact]
    public void Floating_Values_Cannot_Be_Searched_For()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["double", "int"], FunctionKind.Free, "double"));

        var rows = ArgumentRowBuilder.ForCall(layout, Call(0, 7, null, null));

        Assert.Equal([false, true, false], rows.Select(r => r.CanFind));
    }

    private static TimeTravelArgumentCall Call(ulong self,
                                               ulong pointer,
                                               ulong? onEntry,
                                               ulong? onReturn,
                                               ulong returnValue = 0,
                                               bool returned = true)
    {
        var integers = new ulong[ArgumentLayout.CapturedSlots];

        integers[0] = self;
        integers[1] = pointer;

        var entries = new ulong?[ArgumentLayout.CapturedSlots];
        var exits = new ulong?[ArgumentLayout.CapturedSlots];

        entries[1] = onEntry;
        exits[1] = onReturn;

        return new TimeTravelArgumentCall(1, 1, returned, true, integers, new ulong[4], entries, exits, returnValue, 0);
    }

    private sealed class CountingCalls(int count) : IReadOnlyList<TimeTravelArgumentCall>
    {
        public int Count { get; } = count;

        public int Reads { get; private set; }

        public TimeTravelArgumentCall this[int index]
        {
            get
            {
                Reads++;

                return Call((ulong)index, 0, null, null);
            }
        }

        public IEnumerator<TimeTravelArgumentCall> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
