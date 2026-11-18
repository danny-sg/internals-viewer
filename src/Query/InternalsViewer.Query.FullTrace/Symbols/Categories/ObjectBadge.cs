using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols.Categories;

public sealed class ObjectBadge(string name, ObjectStage stage, int index, int count)
{
    public string Name { get; } = name;

    public ObjectStage Stage { get; } = stage;

    public int Index { get; } = index;

    public int Count { get; } = count;

    public string StageName => Stage.GetCategoryMetadata()?.Name ?? Stage.ToString();
}
