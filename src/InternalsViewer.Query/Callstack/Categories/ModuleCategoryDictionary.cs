namespace InternalsViewer.Query.Callstack.Categories;

internal static class ModuleCategoryDictionary
{
    private static readonly Dictionary<string, ModuleCategory> Categories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["sqlmin"] = ModuleCategory.StorageEngine,
            ["sqllang"] = ModuleCategory.QueryProcessor,
            ["SqlDK"] = ModuleCategory.SqlOs,
            ["sqlservr"] = ModuleCategory.SqlServerHost,
            ["SqlTsEs"] = ModuleCategory.ExpressionServices,
            ["kernel32"] = ModuleCategory.System,
            ["ntdll"] = ModuleCategory.System
        };

    public static ModuleCategory GetCategory(
        string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return ModuleCategory.Unknown;
        }

        return Categories.TryGetValue(module,
out var category)
            ? category
            : ModuleCategory.Unknown;
    }
}