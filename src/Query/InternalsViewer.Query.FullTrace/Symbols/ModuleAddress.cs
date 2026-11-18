namespace InternalsViewer.Query.FullTrace.Symbols;

internal readonly record struct ModuleAddress(string Module, uint Rva, string? Symbol, uint Offset)
{
    private const string StringSymbol = "`string'";

    private const string StringDecoratedPrefix = "??_C@";

    public bool IsString
        => Offset == 0
           && Symbol is { } symbol
           && (symbol == StringSymbol || symbol.StartsWith(StringDecoratedPrefix, StringComparison.Ordinal));

    public override string ToString()
        => Symbol is null
            ? $"{Module}+0x{Rva:X}"
            : Offset == 0
                ? $"{Module}!{Symbol}"
                : $"{Module}!{Symbol}+0x{Offset:X}";
}
