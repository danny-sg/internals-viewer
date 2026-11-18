using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols.Categories;

public sealed class ObjectCategoryTable
{
    private const string ObjectsFile = "ObjectCategories.txt";

    private static readonly Lazy<ObjectCategoryTable> DefaultLazy = new(LoadDefault);

    private ObjectCategoryTable(List<ObjectCategoryRule> rules)
    {
        Rules = rules;
    }

    public static ObjectCategoryTable Default => DefaultLazy.Value;

    private List<ObjectCategoryRule> Rules { get; }

    public List<ObjectBadge> GetBadges(string? className)
        => string.IsNullOrEmpty(className) ? [] : [.. Rules.Where(r => r.Class.Matches(className)).Select(r => r.Badge).Distinct()];

    public static ObjectCategoryTable Load(TextReader reader)
    {
        var rows = MappingParser.ParseObjects(reader).ToList();

        var names = rows.DistinctBy(r => (r.Stage, r.Badge)).ToList();

        var counts = names.CountBy(r => r.Stage).ToDictionary();

        var indexes = new Dictionary<ObjectStage, int>();

        var badges = new Dictionary<(ObjectStage, string), ObjectBadge>();

        foreach (var (_, name, stage) in names)
        {
            var index = indexes.GetValueOrDefault(stage);

            indexes[stage] = index + 1;

            badges[(stage, name)] = new ObjectBadge(name, stage, index, counts[stage]);
        }

        return new ObjectCategoryTable([.. rows.Select(r => new ObjectCategoryRule(r.Class, badges[(r.Stage, r.Badge)]))]);
    }

    private static ObjectCategoryTable LoadDefault()
    {
        using var reader = CategoryRuleTable.OpenResource(typeof(ObjectCategoryTable).Assembly, ObjectsFile);

        return Load(reader);
    }

    private sealed record ObjectCategoryRule(GlobPattern Class, ObjectBadge Badge);
}
