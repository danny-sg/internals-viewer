using System.Reflection;
using InternalsViewer.UI.App.Helpers.Selectors;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Tests.Helpers.Selectors;

/// <summary>
/// Checks the selector's templates against the resource dictionary that fills them
/// </summary>
/// <remarks>
/// A template the selector can return but XAML never assigns is null at run time, and WinUI reports that as "Null encountered as data
/// template" without naming which one, only once a step of that kind actually happens. Both halves are assigned by hand for every
/// operator added, so they are compared here instead.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Area", "Helpers")]
public class AccessStepTemplateSelectorTests
{
    [Fact]
    public void Every_Template_The_Selector_Can_Return_Is_Assigned_In_Xaml()
    {
        var xaml = ReadTemplates();

        var missing = TemplateNames().Where(n => !xaml.Contains($"{n}=\"{{StaticResource {n}}}\"")).ToList();

        Assert.True(missing.Count == 0,
                    $"Not assigned on the AccessStepTemplateSelector declaration: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_Template_The_Selector_Can_Return_Is_Defined_In_Xaml()
    {
        var xaml = ReadTemplates();

        var missing = TemplateNames().Where(n => !xaml.Contains($"<DataTemplate x:Key=\"{n}\"")).ToList();

        Assert.True(missing.Count == 0, $"No DataTemplate defined in AccessStepTemplates.xaml: {string.Join(", ", missing)}");
    }

    private static List<string> TemplateNames()
        => [.. typeof(AccessStepTemplateSelector)
               .GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(p => p.PropertyType == typeof(DataTemplate))
               .Select(p => p.Name)];

    private static string ReadTemplates()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Helpers", "Selectors", "AccessStepTemplates.xaml"));
}
