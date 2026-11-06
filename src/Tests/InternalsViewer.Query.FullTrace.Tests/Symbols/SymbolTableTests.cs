using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests.Symbols;

[Trait("Category", "Unit")]
public class SymbolTableTests
{
    private const string Startup = "Startup(BaseXact *)";

    private const string StartupDecorated = "?Startup@@YAXPEAVBaseXact@@@Z";

    private const string StartupQuery = "CQueryScan::StartupQuery(BaseXact *)";

    private const string StartupQueryDecorated = "?StartupQuery@CQueryScan@@QEAAHPEAVBaseXact@@@Z";

    [Fact]
    public void Symbols_Across_Several_Chunks_Keep_The_Order_They_Were_Added_And_Their_Text()
    {
        var builder = new SymbolTable.Builder();

        for (var index = 1200; index > 0; index--)
        {
            builder.Add((uint)(index * 0x10), true, $"Name{index}", $"Signature{index}", $"Decorated{index}");
        }

        var table = builder.Build();

        var reader = table.Read();

        for (var index = 0; index < table.Count; index++)
        {
            var symbol = table.GetSymbol(index);

            var number = table.Count - index;

            Assert.True(reader.MoveNext());

            Assert.Equal((uint)(number * 0x10), symbol.Rva);
            Assert.Equal(($"Name{number}", $"Signature{number}"), (symbol.Name, symbol.Signature));
            Assert.Equal(symbol.Name, reader.Name.ToString());
            Assert.Equal(symbol.Signature, reader.Signature.ToString());
            Assert.Equal($"Decorated{number}", reader.DecoratedName.ToString());
        }

        Assert.False(reader.MoveNext());
        Assert.Equal("Name7", table.FindFunction(0x70)?.Name);
        Assert.Equal("Name1200", table.FindFunction(1200 * 0x10)?.Name);
    }

    [Fact]
    public void Functions_Added_Apart_At_One_Address_Fold_Against_The_First_Added()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x100, true, "Startup", Startup, StartupDecorated);
        builder.Add(0x200, true, "CQueryScan::StartupQuery", StartupQuery, StartupQueryDecorated);
        builder.Add(0x100, true, "Begin", "Begin(BaseXact *)", "?Begin@@YAXPEAVBaseXact@@@Z");

        var reader = builder.Build().Read();

        var functions = new List<(string, bool, bool)>();

        while (reader.MoveNext())
        {
            functions.Add((reader.Name.ToString(), reader.IsFirst, reader.HasFoldedName));
        }

        Assert.Equal([("Startup", true, true), ("CQueryScan::StartupQuery", true, false), ("Begin", false, false)], functions);
    }

    [Fact]
    public void Functions_Folded_At_One_Address_Are_Summarised_On_The_First()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x100, false, "Startup`vftable'", "Startup`vftable'", "??_7Startup@@6B@");
        builder.Add(0x100, true, "Startup", Startup, StartupDecorated);
        builder.Add(0x100, true, "Startup", "Startup(HkTransaction *)", "?Startup@@YAXPEAVHkTransaction@@@Z");
        builder.Add(0x100, true, "Begin", "Begin(BaseXact *)", "?Begin@@YAXPEAVBaseXact@@@Z");

        var reader = builder.Build().Read();

        var functions = new List<(bool, bool, bool)>();

        while (reader.MoveNext())
        {
            if (reader.IsFunction)
            {
                functions.Add((reader.IsFirst, reader.HasFoldedName, reader.HasFoldedParameters));
            }
        }

        Assert.Equal([(true, true, true), (false, false, false), (false, false, false)], functions);
    }

    [Fact]
    public void A_Function_Of_Another_Class_Folded_In_Is_Marked_On_The_First()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x100, true, "Page::Fix", "Page::Fix(int)", "?Fix@Page@@QEAAXH@Z");
        builder.Add(0x100, true, "Page::Unfix", "Page::Unfix(int)", "?Unfix@Page@@QEAAXH@Z");
        builder.Add(0x200, true, "Page::Latch", "Page::Latch(int)", "?Latch@Page@@QEAAXH@Z");
        builder.Add(0x200, true, "Buffer::Latch", "Buffer::Latch(int)", "?Latch@Buffer@@QEAAXH@Z");
        builder.Add(0x300, true, "Page::Steal", "Page::Steal(int)", "?Steal@Page@@QEAAXH@Z");
        builder.Add(0x300, true, "Steal", "Steal(int)", "?Steal@@YAXH@Z");

        var reader = builder.Build().Read();

        var firsts = new List<(bool, bool)>();

        while (reader.MoveNext())
        {
            if (reader.IsFirst)
            {
                firsts.Add((reader.HasFoldedName, reader.HasFoldedClass));
            }
        }

        Assert.Equal([(true, false), (true, true), (true, true)], firsts);
    }

    [Fact]
    public void Signatures_Are_Parsed_When_The_Table_Is_Built()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x100, true, "CQueryScan::StartupQuery", StartupQuery, StartupQueryDecorated);
        builder.Add(0x200, true, "Startup", Startup, StartupDecorated);
        builder.Add(0x300, true, "CQScanNew::CQScanNew", "CQScanNew::CQScanNew(void)", "??0CQScanNew@@QEAA@XZ");
        builder.Add(0x400, true, "Count", "Count(int)", "?Count@@YAXH@Z");

        var table = builder.Build();

        var (member, free, constructor) = (table.GetSymbol(0), table.GetSymbol(1), table.GetSymbol(2));

        var expected = FunctionSignature.Parse(StartupQuery, StartupQueryDecorated)!;

        var parsed = member.Parsed!;

        Assert.Equal(expected.Parameters, parsed.Parameters);
        Assert.Equal((expected.Type, expected.ReturnType, expected.HasReturnBuffer), (parsed.Type, parsed.ReturnType, parsed.HasReturnBuffer));
        Assert.Same(parsed.Parameters[0], free.Parsed!.Parameters[0]);
        Assert.Equal((FunctionType.Member, FunctionType.Free, FunctionType.Member),
                     (member.FunctionType, free.FunctionType, constructor.FunctionType));
        Assert.Equal(("CQueryScan", "StartupQuery"), (member.ClassName, member.MethodName));

        var reader = table.Read();

        var facts = new List<(bool, int?)>();

        while (reader.MoveNext())
        {
            facts.Add((reader.IsConstructor, reader.ParameterCount));
        }

        Assert.Equal([(false, 1), (false, 1), (true, 0), (false, 1)], facts);
    }

    [Fact]
    public void Class_Names_Are_Stored_Once_And_List_Their_Members()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x100, true, "Page::Fix", "Page::Fix(int)", "?Fix@Page@@QEAAXH@Z");
        builder.Add(0x200, true, "Page::Unfix", "Page::Unfix(void)", "?Unfix@Page@@QEAAXXZ");
        builder.Add(0x300, false, "Page::`vftable'", "Page::`vftable'", "??_7Page@@6B@");
        builder.Add(0x400, true, "Startup", Startup, StartupDecorated);

        var table = builder.Build();

        Assert.Same(table.GetSymbol(0).ClassName, table.GetSymbol(1).ClassName);
        Assert.Equal(["Fix", "Unfix"], table.GetClassMembers("Page").Select(s => s.MethodName));
        Assert.Equal(string.Empty, table.GetSymbol(3).ClassName);
        Assert.Empty(table.GetClassMembers("Missing"));
    }

    [Fact]
    public void A_Function_Is_Found_By_Its_Address_Preferring_The_Name_Given()
    {
        var builder = new SymbolTable.Builder();

        builder.Add(0x100, true, "Startup", Startup, StartupDecorated);
        builder.Add(0x100, true, "Begin", "Begin(BaseXact *)", "?Begin@@YAXPEAVBaseXact@@@Z");
        builder.Add(0x200, false, "g_Counter", "g_Counter", "?g_Counter@@3HA");

        var table = builder.Build();

        Assert.Equal("Startup", table.FindFunction(0x100)?.Name);
        Assert.Equal("Begin", table.FindFunction(0x100, "Begin")?.Name);
        Assert.Equal("Startup", table.FindFunction(0x100, "Missing")?.Name);
        Assert.Null(table.FindFunction(0x200));
        Assert.Null(table.FindFunction(0x300));
    }
}
