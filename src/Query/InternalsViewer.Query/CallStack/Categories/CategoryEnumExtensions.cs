namespace InternalsViewer.Query.CallStack.Categories;

public static class CategoryEnumExtensions
{
    public static CategoryAttribute? GetCategoryMetadata(this Enum value)
    {
        var member = value.GetType()
            .GetMember(value.ToString())
            .FirstOrDefault();

        return member?.GetCustomAttributes(typeof(CategoryAttribute), false)
            .Cast<CategoryAttribute>()
            .FirstOrDefault();
    }
}