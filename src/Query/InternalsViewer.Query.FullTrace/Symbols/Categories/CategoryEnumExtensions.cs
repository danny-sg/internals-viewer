using System.Reflection;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols.Categories;

public static class CategoryEnumExtensions
{
    public static CategoryAttribute? GetCategoryMetadata<T>(this T value) where T : struct, Enum
        => CategoryLookup<T>.Attributes.GetValueOrDefault(value);

    public static bool IsInstrumentation(this SymbolCategory category, bool hasOperatorName)
        => category is SymbolCategory.XEventInfrastructure || (category is SymbolCategory.Tracing && !hasOperatorName);

    private static class CategoryLookup<T> where T : struct, Enum
    {
        public static Dictionary<T, CategoryAttribute?> Attributes { get; }
            = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
                       .DistinctBy(f => (T)f.GetValue(null)!)
                       .ToDictionary(f => (T)f.GetValue(null)!, f => f.GetCustomAttribute<CategoryAttribute>(false));
    }
}
