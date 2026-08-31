using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class CallstackResolverTests
{
    private static CallstackFrame CreateFrame(string pdb = "sqlmin.pdb",
                                               string guid = "12345678-1234-1234-1234-123456789012",
                                               int age = 1,
                                               uint rva = 0x1234) =>
        new()
        {
            Module = "sqlmin",
            Pdb = pdb,
            Guid = guid,
            Age = age,
            Rva = rva
        };

    [Fact]
    public void TryResolve_Returns_False_When_Pdb_Is_Empty()
    {
        using var resolver = new CallstackResolver(Path.GetTempPath());

        var result = resolver.TryResolve(CreateFrame(pdb: ""), out var symbol);

        Assert.False(result);
        Assert.Null(symbol);
    }

    [Fact]
    public void TryResolve_Returns_False_When_Guid_Is_Empty()
    {
        using var resolver = new CallstackResolver(Path.GetTempPath());

        var result = resolver.TryResolve(CreateFrame(guid: ""), out var symbol);

        Assert.False(result);
        Assert.Null(symbol);
    }

    [Fact]
    public void TryResolve_Returns_False_When_Guid_Is_AllZero()
    {
        using var resolver = new CallstackResolver(Path.GetTempPath());

        var result = resolver.TryResolve(CreateFrame(guid: "00000000-0000-0000-0000-000000000000"),
                                         out var symbol);

        Assert.False(result);
        Assert.Null(symbol);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryResolve_Returns_False_When_Age_Is_Not_Positive(int age)
    {
        using var resolver = new CallstackResolver(Path.GetTempPath());

        var result = resolver.TryResolve(CreateFrame(age: age), out var symbol);

        Assert.False(result);
        Assert.Null(symbol);
    }

    [Fact]
    public void TryResolve_Returns_Fallback_Hex_When_Pdb_File_Missing_On_Disk()
    {
        var symbolsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        using var resolver = new CallstackResolver(symbolsPath);

        var result = resolver.TryResolve(CreateFrame(rva: 0xABCDEF), out var symbol);

        Assert.True(result);
        Assert.Equal("0xABCDEF", symbol);
    }

    [Fact]
    public void TryResolve_Fallback_Result_Is_Consistent_Across_Repeated_Calls()
    {
        var symbolsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        using var resolver = new CallstackResolver(symbolsPath);

        var frame = CreateFrame(rva: 0x10);

        var first = resolver.TryResolve(frame, out var firstSymbol);
        var second = resolver.TryResolve(frame, out var secondSymbol);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(firstSymbol, secondSymbol);
    }

    [Fact]
    public void TryResolve_Different_Frames_Produce_Independent_Fallback_Results()
    {
        var symbolsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        using var resolver = new CallstackResolver(symbolsPath);

        resolver.TryResolve(CreateFrame(rva: 0x10), out var symbolA);
        resolver.TryResolve(CreateFrame(rva: 0x20), out var symbolB);

        Assert.Equal("0x10", symbolA);
        Assert.Equal("0x20", symbolB);
        Assert.NotEqual(symbolA, symbolB);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times_Without_Throwing()
    {
        var resolver = new CallstackResolver(Path.GetTempPath());

        resolver.Dispose();
        resolver.Dispose();
    }
}
