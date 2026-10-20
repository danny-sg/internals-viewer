using InternalsViewer.Query.FullTrace;
using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.Results;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class ArgumentRowBuilderTests
{
    private static readonly ArgumentLayout SetRange =
        ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *"], FunctionType.Member, "void"));

    [Fact]
    public void Floating_Arguments_Have_No_Column_And_Stack_Arguments_Do()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["double", "int", "int", "int", "int"], FunctionType.Free, "double"));

        var table = ArgumentRowBuilder.BuildTable(layout, [Call(0, 7)]);

        Assert.Equal(["RDX", "R8", "R9", "[RSP+0x28]"], table.Columns.Skip(2).Select(c => c.Detail));
    }

    [Fact]
    public void A_Stack_Argument_Takes_Its_Place_In_The_Signature_And_The_Table()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "int", "int", "int"], FunctionType.Member, "void"));

        var call = Call(0x1_0000, 1) with { Locators = [new Locator(4, LocatorValueType.Argument, 7)] };

        Assert.Equal("F(this 0x10000, 1, 0, 0, 7)", Text(ArgumentRowBuilder.GetSignature("F", layout, call)));
        Assert.Equal("7", Assert.Single(ArgumentRowBuilder.BuildTable(layout, [call]).Rows)[6]);
    }

    [Fact]
    public void A_Stack_Argument_That_Was_Not_Read_Says_So()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "int", "int", "int"], FunctionType.Member, "void"));

        Assert.Equal("Not Captured", Assert.Single(ArgumentRowBuilder.BuildTable(layout, [Call(0x1_0000, 1)]).Rows)[6]);
    }

    [Fact]
    public void A_Void_Function_Has_No_Return()
    {
        var table = ArgumentRowBuilder.BuildTable(SetRange, [Call(0x100, 0x200)]);

        Assert.DoesNotContain(table.Columns, c => c.Name == "Return");
    }

    [Fact]
    public void A_Call_That_Did_Not_Return_Says_So()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Free, "int"));

        var table = ArgumentRowBuilder.BuildTable(layout, [Call(0, 0, returned: false)]);

        Assert.Equal("Did Not Return", Assert.Single(table.Rows)[2]);
    }

    [Fact]
    public void Each_Call_Is_A_Row_With_A_Column_Per_Value()
    {
        List<TraceArgumentCall> calls = [Call(0x100, 0x200)];

        var table = ArgumentRowBuilder.BuildTable(SetRange, calls);

        Assert.Equal(["Call", "Thread", "this", ""], table.Columns.Select(c => c.Name));

        var row = Assert.Single(table.Rows);

        Assert.Equal([1L, 1u, "0x100", "0x200"], Enumerable.Range(0, row.FieldCount).Select(i => row[i]));
    }

    [Fact]
    public void Argument_Columns_Carry_Their_Type_And_Where_They_Are_Read()
    {
        var table = ArgumentRowBuilder.BuildTable(SetRange, [Call(0x100, 0x200)]);

        var argument = table.Columns[3];

        Assert.Equal(("unsigned __int64 *", "RDX"), (argument.TypeName, argument.Detail));
    }

    [Fact]
    public void Values_At_Return_Are_Tinted()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *"], FunctionType.Member, "int"));

        var table = ArgumentRowBuilder.BuildTable(layout, [Call(0x100, 0x200, returnValue: 3)]);

        Assert.Equal(["Return RAX"], table.Columns.Where(c => c.BackgroundColour is not null).Select(c => $"{c.Name} {c.Detail}"));
    }

    [Fact]
    public void Every_Call_Becomes_A_Row()
    {
        List<TraceArgumentCall> calls = [.. Enumerable.Range(1, 5).Select(i => Call((ulong)i, 0))];

        var table = ArgumentRowBuilder.BuildTable(SetRange, calls);

        Assert.Equal([1L, 2L, 3L, 4L, 5L], table.Rows.Select(r => r[0]));
        Assert.Equal([0L, 1L, 2L, 3L, 4L], table.Rows.Select(r => r.Id));
    }

    [Fact]
    public void A_Call_Is_Only_Read_When_Its_Row_Is_Shown()
    {
        var calls = new CountingCalls(10_000);

        var table = ArgumentRowBuilder.BuildTable(SetRange, calls);

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

        var values = Enumerable.Range(0, 5).Select(i => ArgumentRowBuilder.GetCellAt(SetRange, call, i).Value);

        Assert.Equal([null, null, 0x100ul, 0x200ul, null], values);
    }

    [Fact]
    public void A_Return_Value_Is_Only_Given_When_The_Call_Returned()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Free, "int"));

        Assert.Equal(7ul, ArgumentRowBuilder.GetCellAt(layout, Call(0, 0, returnValue: 7), 2).Value);
        Assert.Null(ArgumentRowBuilder.GetCellAt(layout, Call(0, 0, returnValue: 7, returned: false), 2).Value);
    }

    [Fact]
    public void A_Page_Argument_Shows_The_Page_It_Points_To()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["PageId const &"], FunctionType.Member, "void"));

        var call = Call(0x100, 0x200) with { Locators = [new Locator(1, LocatorValueType.Page, 0x0000_0001_0000_0150)] };

        var table = ArgumentRowBuilder.BuildTable(layout, [call]);

        Assert.Equal(new PageAddress(1, 336), Assert.Single(table.Rows)[3]);
        var cell = ArgumentRowBuilder.GetCellAt(layout, call, 3);

        Assert.Equal((0x200ul, LocatorValueType.Page), (cell.Value, cell.Locator?.ValueType));
    }

    [Fact]
    public void A_Row_Returned_By_A_Call_Shows_On_The_Return()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Member, "RowId const *"));

        var call = Call(0x100, 0, returnValue: 0x300) with
        {
            Locators = [new Locator(ArgumentLayout.ReturnRegister, LocatorValueType.Row, 0x0005_0001_0000_0150)]
        };

        var table = ArgumentRowBuilder.BuildTable(layout, [call]);

        Assert.Equal("(1:336:5)", Assert.Single(table.Rows)[3]?.ToString());
    }

    [Fact]
    public void A_Columnstore_Object_Shows_What_It_Names()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["ColumnStoreObjectKey const &"], FunctionType.Member, "void"));

        var key = ColumnStoreObjectLocator.Create(1, 5 | (0x02A7ul << 32), 0x0100 | (2ul << 32), 3 | (1ul << 32));

        var call = Call(0x100, 0x200) with { Locators = [key] };

        Assert.Equal(LocatorValueType.ColumnStoreObject, ArgumentRowBuilder.GetCellAt(layout, call, 3).Locator?.ValueType);
        Assert.Equal("Segment (Rowgroup 3, Column 2)", Assert.Single(ArgumentRowBuilder.BuildTable(layout, [call]).Rows)[3]);
    }

    [Fact]
    public void A_Reference_Shows_The_Value_It_Refers_To_But_Is_Not_A_Locator()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["enum LCK_MODE &"], FunctionType.Member, "void"));

        var call = Call(0x100, 0x200) with { Locators = [new Locator(1, LocatorValueType.Referenced, 4)] };

        Assert.Equal("U", Assert.Single(ArgumentRowBuilder.BuildTable(layout, [call]).Rows)[3]);

        var cell = ArgumentRowBuilder.GetCellAt(layout, call, 3);

        Assert.Equal((0x200ul, (LocatorValueType?)null, "enum LCK_MODE &"), (cell.Value, cell.Locator?.ValueType, cell.Type));
    }

    [Fact]
    public void A_Call_Reads_As_Its_Signature_With_The_Values_In_Place()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *", "int"], FunctionType.Member, "void"));

        var call = new TraceArgumentCall(new TracePosition(1, 0), 1, true, [0x1_F000, 0x2_F000, 5, 0], 0, 0);

        var parts = ArgumentRowBuilder.GetSignature("HoBtAccess::Init", layout, call);

        Assert.Equal("HoBtAccess::Init(this 0x1F000, 0x2F000, 5)", Text(parts));
    }

    [Fact]
    public void Pointers_Can_Be_Found_But_Small_Values_Cannot()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *", "int"], FunctionType.Member, "void"));

        var call = new TraceArgumentCall(new TracePosition(1, 0), 1, true, [0x1_F000, 0x2_F000, 5, 0], 0, 0);

        var found = ArgumentRowBuilder.GetSignature("F", layout, call).Where(p => p.CanFind).Select(p => p.Value);

        Assert.Equal([0x1_F000ul, 0x2_F000ul], found);
    }

    [Fact]
    public void A_Null_Pointer_Has_Nothing_To_Find()
    {
        Assert.DoesNotContain(ArgumentRowBuilder.GetSignature("F", SetRange, Call(0, 0)), p => p.CanFind);
    }

    [Fact]
    public void A_Returned_Pointer_Follows_The_Call()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Member, "HoBtAccess *"));

        var parts = ArgumentRowBuilder.GetSignature("F", layout, Call(0x1_0000, 0, returnValue: 0x3_0000));

        Assert.Equal("F(this 0x10000)  \u2192  0x30000", Text(parts));
        Assert.True(parts[^1].CanFind);
    }

    [Fact]
    public void An_Argument_That_Was_Not_Captured_Shows_Its_Type()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["double"], FunctionType.Free, "void"));

        var parameter = ArgumentRowBuilder.GetSignature("F", layout, Call(0, 0))[2];

        Assert.Equal(("double", CallSignaturePartKind.NotCaptured), (parameter.Text, parameter.Kind));
    }

    [Fact]
    public void Without_A_Signature_The_Registers_Are_Listed_Raw()
    {
        var parts = ArgumentRowBuilder.GetSignature("F", null, Call(0x2_0000, 5, returnValue: 0x3_0000));

        Assert.Equal("F(0x20000, 0x5, 0x0, 0x0)  \u2192  0x30000", Text(parts));
    }

    [Fact]
    public void A_Described_Value_Shows_What_It_Stands_For()
    {
        var layout = KnownParameters.Apply(ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64"], FunctionType.Member, "void")),
                                           "HoBtAccess",
                                           "Init");

        var table = ArgumentRowBuilder.BuildTable(layout, [Call(0x100, 72057594043236352)], (_, _, _) => "dbo.T.PK");

        Assert.Equal("dbo.T.PK", Assert.Single(table.Rows)[3]);
    }

    [Fact]
    public void Excluded_Calls_Are_Left_Out_Of_The_Table()
    {
        List<TraceArgumentCall> calls = [.. Enumerable.Range(0, 5).Select(i => Call((ulong)i, 0))];

        var table = ArgumentRowBuilder.BuildTable(SetRange, calls, included: [1, 3]);

        Assert.Equal([1L, 3L], table.Rows.Select(r => ((ResultRow<long>)r).Id));
        Assert.Equal([2L, 4L], table.Rows.Select(r => r[0]));
    }

    [Fact]
    public void A_Pointer_To_Another_Object_Is_Decoded_With_Its_Class()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["HoBtAccess *"], FunctionType.Member, "void"));

        var parts = ArgumentRowBuilder.GetSignature("F", layout, Call(0x1_0000, 0x2_0000), (slot, _, _) => slot.Type == "this" ? null : "dbo.T.PK");

        Assert.Equal("F(this 0x10000, HoBtAccess dbo.T.PK)", Text(parts));
    }

    [Fact]
    public void A_Page_Is_Decoded_Without_A_Class()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["PageId const &"], FunctionType.Member, "void"));

        var call = Call(0x100, 0x200) with { Locators = [new Locator(1, LocatorValueType.Page, 0x0000_0001_0000_0150)] };

        var page = ArgumentRowBuilder.GetSignature("F", layout, call)[^2];

        Assert.Equal((new PageAddress(1, 336).ToString(), (string?)null), (page.GetText(decode: true), page.DecodedClass));
    }

    [Fact]
    public void A_String_Is_Shown_Without_Quotes()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["char const *"], FunctionType.Static, "void"));

        var call = Call(0x1_0000, 0) with { Locators = [new Locator(0, LocatorValueType.Text, 0x1_0000, 1, "dbo.Orders")] };

        var table = ArgumentRowBuilder.BuildTable(layout, [call]);

        Assert.Equal(new StringValue("dbo.Orders", IsTruncated: true), Assert.Single(table.Rows)[2]);
        Assert.Equal("dbo.Orders (Truncated)", ArgumentRowBuilder.GetSignature("F", layout, call)[^2].GetText(decode: true));
    }

    [Fact]
    public void Types_Are_Carried_With_The_Values_But_Not_With_This()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["bool", "int"], FunctionType.Member, "void"));

        var call = new TraceArgumentCall(new TracePosition(1, 0), 1, true, [0x1_0000, 1, 5, 0], 0, 0);

        var parts = ArgumentRowBuilder.GetSignature("F", layout, call, includeTypes: true);

        Assert.Equal([null, "bool", "int"], parts.Where(p => p.Kind == CallSignaturePartKind.Value).Select(p => p.TypeName));
    }

    [Theory]
    [InlineData(null, "F(this, 0x5)")]
    [InlineData("dbo.T.PK", "F(this dbo.T.PK, 0x5)")]
    public void This_Shows_Only_What_It_Decodes_To(string? reading, string expected)
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int"], FunctionType.Member, "void"));

        var call = new TraceArgumentCall(new TracePosition(1, 0), 1, true, [0x1_0000, 5, 0, 0], 0, 0);

        ValueDescription? Describe(ArgumentSlot slot, ulong value, TraceArgumentCall _) => slot.Type == "this" ? reading : null;

        var parts = ArgumentRowBuilder.GetSignature("F", layout, call, Describe, showThisAddress: false);

        Assert.Equal((expected, "0x10000"), (Text(parts, decode: true), ArgumentRowBuilder.ThisOf(layout, call, Describe)?.Text));
    }

    [Fact]
    public void A_Signature_Reads_With_Its_Types_As_It_Is_Shown()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["bool", "int"], FunctionType.Member, "void"));

        var call = new TraceArgumentCall(new TracePosition(1, 0), 1, true, [0x1_0000, 1, 5, 0], 0, 0);

        var parts = ArgumentRowBuilder.GetSignature("F", layout, call, includeTypes: true);

        Assert.Equal("F(this 0x10000, bool 0x1, int 0x5)", string.Concat(parts.Select(p => p.GetDisplayText(decode: false))));
    }

    [Fact]
    public void Each_Value_Is_Labelled_With_Its_Argument_Number()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["bool", "int"], FunctionType.Member, "int"));

        var call = new TraceArgumentCall(new TracePosition(1, 0), 1, true, [0x1_0000, 1, 5, 0], 9, 0);

        var parts = ArgumentRowBuilder.GetSignature("F", layout, call);

        Assert.Equal(["this", "1", "2", "Return"], parts.Where(p => p.Kind == CallSignaturePartKind.Value).Select(p => p.Label));
    }

    [Fact]
    public void A_Return_Left_Off_The_Signature_Is_Still_Found()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Member, "int"));

        var call = Call(0x1_0000, 0, returnValue: 7);

        var parts = ArgumentRowBuilder.GetSignature("F", layout, call, includeReturn: false);

        Assert.Equal(("F(this 0x10000)", "0x7", "int"),
                     (Text(parts, decode: false), ArgumentRowBuilder.ReturnOf(layout, call, includeType: true)?.Text,
                      ArgumentRowBuilder.ReturnOf(layout, call, includeType: true)?.TypeName));
    }

    [Fact]
    public void A_Pointer_To_A_Known_Object_Takes_The_Object_Class()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["CQScanNew *"], FunctionType.Member, "void"));

        var parts = ArgumentRowBuilder.GetSignature("F",
                                                 layout,
                                                 Call(0x1_0000, 0x2_0000),
                                                 (slot, _, _) => slot.Type == "this" ? null : new ValueDescription("#1", "CQScanRangeNew"));

        Assert.Equal("F(this 0x10000, CQScanRangeNew #1)", Text(parts));
    }

    [Fact]
    public void This_Is_Decoded_Without_Its_Class()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Member, "void"));

        var parts = ArgumentRowBuilder.GetSignature("HoBtAccess::Close", layout, Call(0x1_0000, 0), (_, _, _) => "dbo.T.PK");

        Assert.Equal("HoBtAccess::Close(this dbo.T.PK)", Text(parts));
    }

    [Fact]
    public void Without_Decoding_Every_Value_Is_Hex()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["HoBtAccess *", "int"], FunctionType.Member, "void"));

        var call = new TraceArgumentCall(new TracePosition(1, 0), 1, true, [0x1_0000, 0x2_0000, 5, 0], 0, 0);

        var parts = ArgumentRowBuilder.GetSignature("F", layout, call, (_, _, _) => "dbo.T.PK");

        Assert.Equal("F(this 0x10000, 0x20000, 0x5)", Text(parts, decode: false));
    }

    [Fact]
    public void Without_Decoding_A_Cell_Is_Hex()
    {
        var layout = KnownParameters.Apply(ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64"], FunctionType.Member, "void")),
                                           "HoBtAccess",
                                           "Init");

        var table = ArgumentRowBuilder.BuildTable(layout,
                                                  [Call(0x100, 72057594043236352)],
                                                  (_, _, _) => "dbo.T.PK",
                                                  isDecoded: () => false);

        Assert.Equal("0x100000000510000", Assert.Single(table.Rows)[3]);
    }

    [Fact]
    public void A_Cell_Pointing_At_Another_Object_Carries_Its_Class()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["HoBtAccess *"], FunctionType.Member, "void"));

        var table = ArgumentRowBuilder.BuildTable(layout, [Call(0x100, 0x2_0000)], (slot, _, _) => slot.Type == "this" ? null : "dbo.T.PK");

        Assert.Equal(new ObjectValue("HoBtAccess", "dbo.T.PK"), Assert.Single(table.Rows)[3]);
    }

    [Fact]
    public void A_Cell_Whose_Object_Is_Mapped_Links_To_Its_Origin()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Member, "void"));

        var origin = new MemoryMapEntry(0x1_0000,
                                        MemoryMapOperation.Constructed,
                                        5,
                                        new TracePosition(5, 0),
                                        0,
                                        TracePosition.Unknown,
                                        1,
                                        3,
                                        "CQScanNew",
                                        null);

        var call = Call(0x1_0000, 0);

        var table = ArgumentRowBuilder.BuildTable(layout, [call], (_, _, _) => new ValueDescription("0x10000") { Origin = origin });

        Assert.Equal(new ObjectValue(null, "0x10000") { Origin = origin }, Assert.Single(table.Rows)[2]);
        Assert.Same(origin, ArgumentRowBuilder.OriginAt(layout, call, 2, (_, _, _) => new ValueDescription("0x10000") { Origin = origin }));
    }

    private static string Text(IEnumerable<CallSignaturePart> parts, bool decode = true)
        => string.Concat(parts.Select(p => p.GetText(decode)));

    private static TraceArgumentCall Call(ulong self, ulong pointer, ulong returnValue = 0, bool returned = true)
        => new(new TracePosition(1, 0), 1, returned, [self, pointer, 0, 0], returnValue, 0);

    private sealed class CountingCalls(int count) : IReadOnlyList<TraceArgumentCall>
    {
        public int Count { get; } = count;

        public int Reads { get; private set; }

        public TraceArgumentCall this[int index]
        {
            get
            {
                Reads++;

                return Call((ulong)index, 0);
            }
        }

        public IEnumerator<TraceArgumentCall> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
