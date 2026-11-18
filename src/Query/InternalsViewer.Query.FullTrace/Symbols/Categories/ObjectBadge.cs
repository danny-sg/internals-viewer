using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols.Categories;

public sealed class ObjectBadge(string name, ObjectStage stage, string colour)
{
    public string Name { get; } = name;

    public ObjectStage Stage { get; } = stage;

    public string Colour { get; } = colour;

    public string StageName => Stage.GetCategoryMetadata()?.Name ?? Stage.ToString();
}
