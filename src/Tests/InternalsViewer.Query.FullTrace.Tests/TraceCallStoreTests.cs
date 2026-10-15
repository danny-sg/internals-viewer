using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.CallStack.Locators;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceCallStoreTests
{
    private const ulong ReturnedFlag = 1ul << 32;

    private const ulong Page336 = 0x0000_0001_0000_0150;

    [Fact]
    public void The_Calls_Of_One_Function_On_One_Instance_Are_Read_Back_In_Order()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(7, rcx: 0xA0)), 1);
        builder.Add(0x2000, 0xB0, Chunk(Row(8, rcx: 0xB0), Row(9, rcx: 0xB0)), 2);

        var calls = builder.Build().GetCallList(0x2000, 0xB0);

        Assert.NotNull(calls);
        Assert.Equal([8ul, 9ul], calls.Select(c => c.Sequence));
        Assert.All(calls, c => Assert.Equal(0xB0ul, c.IntegerSlots[0]));
    }

    [Fact]
    public void Calls_Logged_In_Several_Chunks_Read_As_One_List()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);
        builder.Add(0x1000, 0, Chunk(Row(3)), 1);

        var calls = builder.Build().GetCallList(0x1000, 0);

        Assert.NotNull(calls);
        Assert.Equal(3, calls.Count);
        Assert.Equal(3ul, calls[2].Sequence);
        Assert.Equal([1ul, 2ul, 3ul], calls.Select(c => c.Sequence));
    }

    [Fact]
    public void A_Function_With_No_Logged_Calls_Reads_As_Nothing()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(7, rcx: 0xA0)), 1);

        Assert.Null(builder.Build().GetCallList(0x1000, 0xB0));
    }

    [Fact]
    public void Repeated_Values_Compress_Well()
    {
        var builder = new TraceCallStore.Builder();

        var calls = 1_000;

        builder.Add(0x1000, 0, Chunk([.. Enumerable.Range(0, calls).Select(i => Row((ulong)i, rcx: 0xA0))]), calls);

        var store = builder.Build();

        Assert.Equal(calls, store.Calls);
        Assert.True(store.Size < calls * TraceArgumentCall.ValueCount * sizeof(ulong) / 10);
    }

    [Fact]
    public void A_Call_Carries_Its_Id_And_The_Id_Current_When_It_Returned()
    {
        var row = Row(1);

        row[8] = 41;

        row[9] = 57;

        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(row), 1);

        var call = builder.Build().GetCallList(0x1000, 0)![0];

        Assert.Equal((41ul, 57ul), (call.Id, call.EndId));
    }

    [Fact]
    public void A_Call_Is_Found_By_Its_Id_Across_Chunks()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(WithId(Row(1), 3), WithId(Row(2), 8)), 2);
        builder.Add(0x1000, 0, Chunk(WithId(Row(3), 20)), 1);

        var calls = builder.Build().GetCallList(0x1000, 0)!;

        Assert.Equal([1, 2, -1, -1], new ulong[] { 8, 20, 9, 1 }.Select(calls.IndexOf));
    }

    [Fact]
    public void The_Enclosing_Call_Is_The_Latest_Started_That_Has_Not_Ended()
    {
        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(WithIds(Row(1), 3, 40), WithIds(Row(2), 5, 9), WithIds(Row(3), 50, 0)), 3);

        var calls = builder.Build().GetCallList(0x1000, 0)!;

        Assert.Equal([0, 1, 0, -1, 2], new ulong[] { 4, 7, 12, 2, 60 }.Select(calls.IndexOfEnclosing));
    }

    [Fact]
    public void A_Chunk_Recorded_Before_Call_Ids_Reads_Without_Them()
    {
        const int calls = 542;

        var random = new Random(1);

        var columns = new ulong[calls * 8];

        for (var index = 0; index < columns.Length; index++)
        {
            columns[index] = (ulong)random.NextInt64(0, 1L << 40);
        }

        using var compressed = new MemoryStream();

        using (var stream = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            stream.Write(MemoryMarshal.AsBytes(columns.AsSpan()));
        }

        var read = TraceCallStore.Decompress(new TraceCallStore.Chunk(calls, compressed.ToArray()));

        Assert.Equal((columns[^1], 0), (read[calls * 8 - 1], read.Skip(calls * 8).Count(v => v != 0)));
    }

    [Fact]
    public void A_String_The_Trace_Never_Read_Is_Read_From_The_Module_File()
    {
        const ulong imageBase = 0x7FF0_0000_0000;

        var path = Path.Combine(Environment.SystemDirectory, "kernel32.dll");

        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 0, false, LocatorValueType.Text)]);

        builder.Add(0x1000, 0, Chunk(Row(1, rcx: imageBase + GetRva(path, "CreateFileW\0"u8))), 1);

        using var images = new ImageText([TraceModule.Read(path, imageBase, 0x1000_0000)]);

        Assert.Equal("CreateFileW", builder.Build(images).GetCallList(0x1000, 0)![0].GetLocator(0)?.ToString());
    }

    [Fact]
    public void A_String_Is_Put_Together_From_Its_Parts_And_Kept_When_Written()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 1, false, LocatorValueType.WideText)]);

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        var parts = TextDecoderTests.Parts(Encoding.Unicode.GetBytes("dbo.Orders\0"));

        builder.AddLocators([.. parts.Select(p => new PointerValue(0x1000, 0, p.Value, 0, 1, Mode: 3, Part: p.Key))]);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            builder.Build().Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var locator = TraceCallStore.Read(reader).GetCallList(0x1000, 0)![0].GetLocator(1);

        Assert.Equal((LocatorValueType.WideText, "dbo.Orders"), (locator?.ValueType, locator?.ToString()));
    }

    [Fact]
    public void A_Log_Written_With_Eight_Slots_Reads_Its_Return_From_The_Return_Slot()
    {
        var register = LocatorReadResolver.ReturnRegister;

        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, register, true, LocatorValueType.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(1, flags: ReturnedFlag)), 1);

        builder.AddLocators([new PointerValue(0x1000, 0, Page336, 0, register, IsReadAtReturn: 1)]);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            builder.Build().Write(writer);
        }

        using var reader = new BinaryReader(new MemoryStream(ToEightSlots(stream.ToArray())));

        var locator = TraceCallStore.Read(reader).GetCallList(0x1000, 0)![0].GetLocator(register);

        Assert.Equal((LocatorValueType.Page, Page336), (locator?.ValueType, locator?.Value));
    }

    [Fact]
    public void A_Type_Info_Is_Put_Together_From_Its_Type_And_Its_Length()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 1, false, LocatorValueType.TypeInfo, 0),
                                                new LocatorRead(0x1000, 1, false, LocatorValueType.TypeInfo, 16)]);

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        builder.AddLocators([new PointerValue(0x1000, 0, 0x0000_00E7_0000_00E7, 0, 1, Mode: 4, Part: 8),
                             new PointerValue(0x1000, 0, 0xFFFF_FFFF_0000_0100, 0, 1, 16, Mode: 4, Part: 8)]);

        Assert.Equal("nvarchar(128)", builder.Build().GetCallList(0x1000, 0)![0].GetLocator(1)?.ToString());
    }

    [Fact]
    public void A_Type_Info_Whose_Length_Was_Never_Read_Shows_Its_Type()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 1, false, LocatorValueType.TypeInfo, 0),
                                                new LocatorRead(0x1000, 1, false, LocatorValueType.TypeInfo, 16)]);

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        builder.AddLocators([new PointerValue(0x1000, 0, 0x0138, 0, 1, Mode: 4, Part: 2)]);

        Assert.Equal("int NOT NULL", builder.Build().GetCallList(0x1000, 0)![0].GetLocator(1)?.ToString());
    }

    [Fact]
    public void A_Log_Sequence_Number_Is_Put_Together_From_Its_Two_Words()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 2, false, LocatorValueType.LogSequenceNumber, 0),
                                                new LocatorRead(0x1000, 2, false, LocatorValueType.LogSequenceNumber, 8)]);

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        builder.AddLocators([new PointerValue(0x1000, 0, 37 | (152ul << 32), 0, 2),
                             new PointerValue(0x1000, 0, 0xFFFF_0000_0002, 0, 2, 8)]);

        var store = builder.Build();

        var locator = store.GetCallList(0x1000, 0)![0].GetLocator(2);

        Assert.Equal((new LogSequenceNumber(37, 152, 2), "(37:152:2)"), (locator?.Lsn, locator?.ToString()));
    }

    [Fact]
    public async Task A_Stack_Argument_Is_Kept_As_A_Value_Beside_The_Page_It_Points_To()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 4, false, LocatorValueType.Argument),
                                                new LocatorRead(0x1000, 4, false, LocatorValueType.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocators([new PointerValue(0x1000, 0, 0x5_0000, 1, 4, Mode: 1),
                             new PointerValue(0x1000, 0, Page336, 1, 4)]);

        var store = builder.Build();

        var call = store.GetCallList(0x1000, 0)![1];

        Assert.Equal(0x5_0000ul, call.GetStackValue(4));
        Assert.Equal(new PageAddress(1, 336), call.GetLocator(4)?.Page);

        var search = new TraceValueSearch(store);

        var use = Assert.Single(await search.GetValueUsesAsync(0x5_0000, CancellationToken.None));

        Assert.Equal(("[RSP+0x28]", 1), (use.Location, use.First.Call));
        Assert.Equal([1], (await search.GetTraceValueCallsAsync(use, 0x5_0000, CancellationToken.None)).Select(c => c.Call));
    }

    [Fact]
    public async Task A_Call_Leads_To_The_Call_Tree_Node_It_Was_Made_On()
    {
        var tree = new CallStackTree();

        var first = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var second = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var builder = new TraceCallStore.Builder();

        builder.Add(0x1000, 0, Chunk(Row(1, rcx: 0xA0, node: 1), Row(2, node: -1)), 2);

        var store = builder.Build();

        store.MapNodes([first, second]);

        var calls = store.GetCallList(0x1000, 0)!;

        Assert.Same(second, store.GetNode(calls[0]));
        Assert.Null(store.GetNode(calls[1]));
        Assert.Same(second, Assert.Single(await new TraceValueSearch(store).GetValueUsesAsync(0xA0, CancellationToken.None)).First.Node);
    }

    [Fact]
    public void A_Page_Read_In_The_Replay_Is_Attached_To_Its_Call()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 2, false, LocatorValueType.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocators([new PointerValue(0x1000, 0, Page336, 1, 2)]);

        var store = builder.Build();

        var calls = store.GetCallList(0x1000, 0)!;

        Assert.Empty(calls[0].Locators);
        Assert.Equal(new PageAddress(1, 336), calls[1].GetLocator(2)?.Page);
        Assert.Null(calls[1].GetLocator(1));
        Assert.Equal(1, store.LocatorCount);
    }

    [Fact]
    public void A_Value_Read_On_Return_Replaces_The_One_Read_When_Called()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 1, false, LocatorValueType.Referenced),
                                                new LocatorRead(0x1000, 1, true, LocatorValueType.Referenced)]);

        builder.Add(0x1000, 0, Chunk(Row(1), Row(2)), 2);

        builder.AddLocators([new PointerValue(0x1000, 0, 3, 0, 1),
                             new PointerValue(0x1000, 0, 3, 1, 1),
                             new PointerValue(0x1000, 0, 5, 1, 1, IsReadAtReturn: 1)]);

        var calls = builder.Build().GetCallList(0x1000, 0)!;

        Assert.Equal(3ul, calls[0].GetLocator(1)?.Value);
        Assert.Equal(5ul, Assert.Single(calls[1].Locators).Value);
    }

    [Fact]
    public void A_Value_Behind_A_Reference_Is_Not_Annotated_On_The_Call_Tree()
    {
        var tree = new CallStackTree();

        var node = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);

        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 1, false, LocatorValueType.Referenced)]);

        builder.Add(0x1000, 0, Chunk(Row(10, node: 0)), 1);

        builder.AddLocators([new PointerValue(0x1000, 0, 3, 0, 1)]);

        var store = builder.Build();

        store.MapNodes([node]);

        store.AnnotateNodes();

        Assert.False(node.HasLocators);
    }

    [Fact]
    public void A_Value_Read_From_A_Register_With_No_Rule_Is_Ignored()
    {
        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 2, false, LocatorValueType.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(1)), 1);

        builder.AddLocators([new PointerValue(0x1000, 0, Page336, 0, 1), new PointerValue(0x2000, 0, Page336, 0, 2)]);

        Assert.Equal(0, builder.Build().LocatorCount);
    }

    [Fact]
    public void The_Pages_A_Call_Read_Are_Annotated_On_Its_Call_Tree_Node()
    {
        var tree = new CallStackTree();

        var first = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var second = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var builder = new TraceCallStore.Builder([new LocatorRead(0x1000, 2, false, LocatorValueType.Page)]);

        builder.Add(0x1000, 0, Chunk(Row(10, node: 0), Row(11, node: 1), Row(12, node: 1)), 3);

        builder.AddLocators([new PointerValue(0x1000, 0, Page336, 0, 2),
                             new PointerValue(0x1000, 0, Page336 + 1, 1, 2),
                             new PointerValue(0x1000, 0, Page336 + 2, 2, 2)]);

        var store = builder.Build();

        store.MapNodes([first, second]);

        store.AnnotateNodes();

        Assert.Equal("(1:336)", first.LocatorsLabel);
        Assert.Equal("2 Pages", second.LocatorsLabel);
        Assert.False(tree.Root.HasLocators);
    }

    private static ulong[] WithId(ulong[] row, ulong id)
    {
        row[8] = id;

        return row;
    }

    private static ulong[] WithIds(ulong[] row, ulong id, ulong endId)
    {
        row[8] = id;
        row[9] = endId;

        return row;
    }

    private static byte[] ToEightSlots(byte[] written)
    {
        Assert.Equal(-2, BitConverter.ToInt32(written));

        var bytes = written[sizeof(int)..];

        byte[] returned = [(byte)LocatorReadResolver.ReturnRegister, 0, 0, 0, (byte)LocatorValueType.Page];

        var register = bytes.AsSpan().LastIndexOf(returned);

        bytes[register] = 8;

        return bytes;
    }

    private static uint GetRva(string path, ReadOnlySpan<byte> text)
    {
        var offset = File.ReadAllBytes(path).AsSpan().IndexOf(text);

        using var stream = File.OpenRead(path);

        var section = new PEHeaders(stream).SectionHeaders.First(s => offset >= s.PointerToRawData
                                                                      && offset < s.PointerToRawData + s.SizeOfRawData);

        return (uint)(section.VirtualAddress + offset - section.PointerToRawData);
    }
}
