using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Tabs;

[Trait("Category", "Unit")]
public class LayoutObjectReaderTests
{
    private const ulong Session = 0x1F4_0000_1000;

    private const ulong Bindings = 0x1F4_0000_8000;

    private const int SessionSize = 0xB90;

    private static readonly EngineLayout Layout = EngineLayout.Latest;

    private static readonly MemoryField HeapRowField = Layout.GetStructure("HeapDataSetSession").Get("Heap Row");

    [Fact]
    public void An_Object_Reads_Its_Embedded_Objects_With_Their_Own_Fields()
    {
        var memory = new FakeTraceReplay().Write(Session + (ulong)(HeapRowField.Offset + Layout.HeapRow.SlotField.Offset), 7);

        var session = ReadSession(memory);

        var row = Assert.Single(session.Children);

        Assert.Equal(("Heap Row", "HeapRowObject", Session + (ulong)HeapRowField.Offset), (row.Name, row.Type, row.Address));

        Assert.Equal(7UL, row.Rows[0][row.Fields.IndexOf(Layout.HeapRow.SlotField)]);
    }

    [Fact]
    public void An_Embedded_Field_Holds_The_Address_Of_Its_Object()
    {
        var session = ReadSession(new FakeTraceReplay());

        Assert.Equal(Session + (ulong)HeapRowField.Offset, session.Rows[0][session.Fields.IndexOf(HeapRowField)]);
    }

    [Fact]
    public void An_Address_Inside_An_Embedded_Object_Finds_That_Object_And_Field()
    {
        var session = ReadSession(new FakeTraceReplay());

        var found = session.Find(Session + (ulong)(HeapRowField.Offset + Layout.HeapRow.SlotField.Offset));

        Assert.NotNull(found);

        Assert.Equal(("Heap Row", found.Value.Object.Fields.IndexOf(Layout.HeapRow.SlotField)), (found.Value.Object.Name, found.Value.Row));
    }

    [Fact]
    public void An_Address_Outside_The_Object_Finds_Nothing()
        => Assert.Null(ReadSession(new FakeTraceReplay()).Find(Session + SessionSize));

    [Fact]
    public void A_Row_Tab_Reads_One_Row_Per_Element_And_Finds_The_Element()
    {
        var binding = Layout.GetStructure("ColumnBinding");

        var stride = binding.Size;

        var ordinal = binding.Get("Ordinal");

        var memory = new FakeTraceReplay().Write(Bindings + (ulong)(2 * stride + ordinal.Offset), (ushort)3);

        var tab = new MemoryTab("Column Bindings", "ColumnBinding", MemoryTabLayout.Row);

        var array = LayoutObjectReader.Read(memory, tab, Bindings, 3 * stride);

        Assert.Equal((3, 3UL), (array.Rows.Count, array.Rows[2][array.Fields.IndexOf(ordinal)]));

        Assert.Equal(2, array.Find(Bindings + (ulong)(2 * stride + ordinal.Offset))?.Row);
    }

    [Fact]
    public void A_Trailing_Array_Shares_The_Element_Count_And_Follows_The_Elements()
    {
        var stride = Layout.GetStructure("ColumnBinding").Size;

        var trailing = Layout.GetStructure("ColumnBindingInfrequent").Size;

        var tab = new MemoryTab("Column Bindings", "ColumnBinding", MemoryTabLayout.Row, "ColumnBindingInfrequent");

        var array = LayoutObjectReader.Read(new FakeTraceReplay(), tab, Bindings, 2 * (stride + trailing));

        var infrequent = Assert.Single(array.Children);

        Assert.Equal((2, "Infrequent Data", 2, Bindings + (ulong)(2 * stride)),
                     (array.Rows.Count, infrequent.Name, infrequent.Rows.Count, infrequent.Address));

        Assert.Same(infrequent, array.Find(Bindings + (ulong)(2 * stride + trailing))?.Object);
    }

    [Fact]
    public void A_Pointer_Found_In_An_Object_Becomes_A_Field_Named_By_Its_Offset_In_The_Innermost_Object()
    {
        var heapRow = (ulong)HeapRowField.Offset;

        var session = ReadSession(new FakeTraceReplay(), [Session + 0x10, Session + heapRow + 0x08]);

        Assert.Equal(0x10, session.Fields.Single(f => f.Name == "Pointer+0x10").Offset);

        Assert.Equal(0x08, session.Children[0].Fields.Single(f => f.Name == "Pointer+0x8").Offset);
    }

    [Fact]
    public void A_Pointer_Inside_A_Known_Field_Adds_No_Field()
    {
        var offset = Layout.HeapRow.PageHandleField.Offset;

        var session = ReadSession(new FakeTraceReplay(), [Session + (ulong)(HeapRowField.Offset + offset)]);

        Assert.DoesNotContain(session.Children[0].Fields, f => f.Name == $"Pointer+0x{offset:X}");
    }

    [Fact]
    public void An_Address_Between_Fields_Finds_The_Object_But_No_Field()
    {
        var session = ReadSession(new FakeTraceReplay(), [Session + 0x10]);

        Assert.Equal((session, -1), session.Find(Session + 0x20));
    }

    private static LayoutObject ReadSession(FakeTraceReplay memory, List<ulong>? pointers = null)
        => LayoutObjectReader.Read(memory,
                                   new MemoryTab("Heap Data Set Session", "HeapDataSetSession", MemoryTabLayout.Column),
                                   Session,
                                   SessionSize,
                                   pointers);
}
