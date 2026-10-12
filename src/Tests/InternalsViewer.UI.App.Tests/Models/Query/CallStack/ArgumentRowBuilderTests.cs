using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class ArgumentRowBuilderTests
{
    private static readonly ArgumentLayout SetRange =
        ArgumentLayout.For(new FunctionSignature(["unsigned __int64 *"], FunctionKind.Member, "void"));

    [Fact]
    public void Stack_And_Floating_Arguments_Have_No_Column()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["double", "int", "int", "int", "int"], FunctionKind.Free, "double"));

        var table = ArgumentRowBuilder.Table(layout, [Call(0, 7)]);

        Assert.Equal(["RDX", "R8", "R9"], table.Columns.Skip(2).Select(c => c.Detail));
    }

    [Fact]
    public void A_Void_Function_Has_No_Return()
    {
        var table = ArgumentRowBuilder.Table(SetRange, [Call(0x100, 0x200)]);

        Assert.DoesNotContain(table.Columns, c => c.Name == "Return");
    }

    [Fact]
    public void A_Call_That_Did_Not_Return_Says_So()
    {
        var layout = ArgumentLayout.For(new FunctionSignature([], FunctionKind.Free, "int"));

        var table = ArgumentRowBuilder.Table(layout, [Call(0, 0, returned: false)]);

        Assert.Equal("Did Not Return", Assert.Single(table.Rows)[2]);
    }

    [Fact]
    public void Each_Call_Is_A_Row_With_A_Column_Per_Value()
    {
        List<FullTraceArgumentCall> calls = [Call(0x100, 0x200)];

        var table = ArgumentRowBuilder.Table(SetRange, calls);

        Assert.Equal(["Call", "Thread", "this", ""], table.Columns.Select(c => c.Name));

        var row = Assert.Single(table.Rows);

        Assert.Equal([1L, 1u, "0x100", "0x200"], Enumerable.Range(0, row.FieldCount).Select(i => row[i]));
    }

    [Fact]
    public void Argument_Columns_Carry_Their_Type_And_Where_They_Are_Read()
    {
        var table = ArgumentRowBuilder.Table(SetRange, [Call(0x100, 0x200)]);

        var argument = table.Columns[3];

        Assert.Equal(("unsigned __int64 *", "RDX"), (argument.TypeName, argument.Detail));
    }

    [Fact]
    public void Values_At_Return_Are_Tinted()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["unsigned __int64 *"], FunctionKind.Member, "int"));

        var table = ArgumentRowBuilder.Table(layout, [Call(0x100, 0x200, returnValue: 3)]);

        Assert.Equal(["Return RAX"], table.Columns.Where(c => c.BackgroundColour is not null).Select(c => $"{c.Name} {c.Detail}"));
    }

    [Fact]
    public void Every_Call_Becomes_A_Row()
    {
        List<FullTraceArgumentCall> calls = [.. Enumerable.Range(1, 5).Select(i => Call((ulong)i, 0))];

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
    public void A_Cell_Gives_The_Value_Behind_It()
    {
        var call = Call(0x100, 0x200);

        var values = Enumerable.Range(0, 5).Select(i => ArgumentRowBuilder.CellAt(SetRange, call, i).Value);

        Assert.Equal([null, null, 0x100ul, 0x200ul, null], values);
    }

    [Fact]
    public void A_Return_Value_Is_Only_Given_When_The_Call_Returned()
    {
        var layout = ArgumentLayout.For(new FunctionSignature([], FunctionKind.Free, "int"));

        Assert.Equal(7ul, ArgumentRowBuilder.CellAt(layout, Call(0, 0, returnValue: 7), 2).Value);
        Assert.Null(ArgumentRowBuilder.CellAt(layout, Call(0, 0, returnValue: 7, returned: false), 2).Value);
    }

    [Fact]
    public void A_Page_Argument_Shows_The_Page_It_Points_To()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["PageId const &"], FunctionKind.Member, "void"));

        var call = Call(0x100, 0x200) with { Locators = [new Locator(1, LocatorKind.Page, 0x0000_0001_0000_0150)] };

        var table = ArgumentRowBuilder.Table(layout, [call]);

        Assert.Equal(new PageAddress(1, 336), Assert.Single(table.Rows)[3]);
        var cell = ArgumentRowBuilder.CellAt(layout, call, 3);

        Assert.Equal((0x200ul, LocatorKind.Page), (cell.Value, cell.Locator?.Kind));
    }

    [Fact]
    public void A_Row_Returned_By_A_Call_Shows_On_The_Return()
    {
        var layout = ArgumentLayout.For(new FunctionSignature([], FunctionKind.Member, "RowId const *"));

        var call = Call(0x100, 0, returnValue: 0x300) with
        {
            Locators = [new Locator(LocatorReadResolver.ReturnRegister, LocatorKind.Row, 0x0005_0001_0000_0150)]
        };

        var table = ArgumentRowBuilder.Table(layout, [call]);

        Assert.Equal("(1:336:5)", Assert.Single(table.Rows)[3]?.ToString());
    }

    [Fact]
    public void A_Columnstore_Object_Shows_What_It_Names()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["ColumnStoreObjectKey const &"], FunctionKind.Member, "void"));

        var key = Locator.ColumnStoreObject(1, 5 | (0x02A7ul << 32), 0x0100 | (2ul << 32), 3 | (1ul << 32));

        var call = Call(0x100, 0x200) with { Locators = [key] };

        Assert.Equal(LocatorKind.ColumnStoreObject, ArgumentRowBuilder.CellAt(layout, call, 3).Locator?.Kind);
        Assert.Equal("Segment (Rowgroup 3, Column 2)", Assert.Single(ArgumentRowBuilder.Table(layout, [call]).Rows)[3]);
    }

    [Fact]
    public void A_Reference_Shows_The_Value_It_Refers_To_But_Is_Not_A_Locator()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["enum LCK_MODE &"], FunctionKind.Member, "void"));

        var call = Call(0x100, 0x200) with { Locators = [new Locator(1, LocatorKind.Referenced, 4)] };

        Assert.Equal("U", Assert.Single(ArgumentRowBuilder.Table(layout, [call]).Rows)[3]);

        var cell = ArgumentRowBuilder.CellAt(layout, call, 3);

        Assert.Equal((0x200ul, (LocatorKind?)null, "enum LCK_MODE &"), (cell.Value, cell.Locator?.Kind, cell.Type));
    }

    private static FullTraceArgumentCall Call(ulong self, ulong pointer, ulong returnValue = 0, bool returned = true)
        => new(1, 1, returned, [self, pointer, 0, 0], returnValue, 0);

    private sealed class CountingCalls(int count) : IReadOnlyList<FullTraceArgumentCall>
    {
        public int Count { get; } = count;

        public int Reads { get; private set; }

        public FullTraceArgumentCall this[int index]
        {
            get
            {
                Reads++;

                return Call((ulong)index, 0);
            }
        }

        public IEnumerator<FullTraceArgumentCall> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
