namespace InternalsViewer.Query.Callstack.Categories;

[AttributeUsage(AttributeTargets.Field)]
public sealed class CategoryAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    public string? Description { get; init; }

    public string? ForegroundColor { get; init; }

    public string? BackgroundColor { get; init; }

    public bool IsInfrastructure { get; init; }
}