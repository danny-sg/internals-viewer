using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace InternalsViewer.UI.App.Controls.Instructions;

public static partial class InstructionsPageProvider
{
    public static string? GetPage(string key)
    {
        var assembly = typeof(InstructionsPageProvider).Assembly;

        var name = $"InternalsViewer.UI.App.Assets.Instructions.{key}.md";

        using var stream = assembly.GetManifestResourceStream(name);

        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public static string Render(string markdown, IReadOnlyDictionary<string, bool> state)
        => TokenRegex().Replace(markdown,
                                match => state.TryGetValue(match.Groups[1].Value, out var value)
                                    ? value ? "x" : " "
                                    : match.Value);

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenRegex();
}
