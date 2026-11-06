namespace UnusedCodeAnalyser;

internal sealed class Declaration
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Display { get; init; }

    public required string Kind { get; init; }

    public required string FilePath { get; init; }

    public required int Line { get; init; }

    public required bool IsGenerated { get; init; }

    public string? ContainingType { get; init; }

    public List<string> Bases { get; } = [];

    public bool HasExternalBase { get; set; }

    public bool IsType { get; init; }

    public bool IsConstructor { get; init; }

    public bool IsAbstract { get; init; }

    public bool IsStatic { get; init; }

    public bool IsPositional { get; init; }

    public bool IsLayout { get; init; }

    public string? BaseTypeId { get; init; }

    public string BaseTypeChain { get; init; } = string.Empty;

    public string Location => $"{FilePath}:{Line}";
}
