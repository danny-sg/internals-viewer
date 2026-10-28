using System.Text.RegularExpressions;

namespace InternalsViewer.Mcp.Prompts;

/// <summary>
/// Fills the {{name}} placeholders of a prompt text from the properties of a values object
/// </summary>
public static partial class PromptTemplate
{
    /// <summary>
    /// Replaces every placeholder with the string value of the property of the same name
    /// </summary>
    /// <remarks>A placeholder with no matching property throws, so a typo cannot reach the model as literal text.</remarks>
    public static string Render<TValues>(string template, TValues values)
        where TValues : notnull
    {
        var properties = typeof(TValues).GetProperties().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        return Placeholder().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;

            return properties.TryGetValue(name, out var property)
                ? property.GetValue(values)?.ToString() ?? string.Empty
                : throw new InvalidOperationException($"The prompt has a placeholder {{{{{name}}}}} with no value.");
        });
    }

    [GeneratedRegex(@"\{\{(?<name>\w+)\}\}")]
    private static partial Regex Placeholder();
}
