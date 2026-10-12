using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ModuleSymbolsTests
{
    private static readonly ModuleSymbols Symbols = new(new Dictionary<uint, string[]>
    {
        [0x10] = ["XeSqlPkg::latch_acquired::Publish"],
        [0x20] = ["XeSqlPkg::lock_acquired::Publish", "XeSqlPkg::lock_acquired::Publish2"],
        [0x30] = ["XeSqlPkg::lock_released::Publish", "XeSqlPkg::wait_info::Publish"],
        [0x40] = ["XeSqlPkg::lock_released::Publish", "SomeClass::Unrelated"]
    });

    [Fact]
    public void Agreed_Keeps_An_Address_Every_Symbol_At_It_Agrees_On()
    {
        var agreed = Symbols.Agreed((_, name) => name.Split("::")[1]);

        Assert.Equal([(0x10u, "latch_acquired"), (0x20u, "lock_acquired")], agreed.OrderBy(a => a.Rva));
    }

    [Fact]
    public void Agreed_Drops_An_Address_Where_A_Folded_Symbol_Has_No_Value()
    {
        var agreed = Symbols.Agreed((_, name) => name.StartsWith("Xe", StringComparison.Ordinal) ? "event" : null);

        Assert.DoesNotContain(agreed, a => a.Rva == 0x40);
    }

    [Fact]
    public void Where_Sees_Every_Symbol_At_The_Address()
    {
        var all = Symbols.Where(names => names.All(n => n.StartsWith("Xe", StringComparison.Ordinal)));

        Assert.Equal([0x10u, 0x20u, 0x30u], all.Order());
    }
}
