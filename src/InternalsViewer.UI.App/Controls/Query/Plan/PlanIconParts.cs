using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

internal static partial class PlanIconParts
{
    private const string PartAttribute = "data-part";

    private const string DisplayAttribute = "display";

    private const string FillAttribute = "fill";

    private const string StrokeAttribute = "stroke";

    private const string None = "none";

    private const char IconSeparator = '.';

    private static readonly HashSet<string> Shapes = ["path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text"];

    private static Dictionary<string, PlanIcon?> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<(string Icon, string Part, Color Colour), Task<SvgImageSource?>> Overlays { get; } = [];

    private static IReadOnlyList<string>? Bindings { get; set; }

    public static IReadOnlyList<string> GetBindings() => Bindings ??= FindBindings();

    public static IReadOnlyList<string> GetParts(string icon, string binding)
    {
        var separator = binding.LastIndexOf(IconSeparator);

        if (separator >= 0 && !string.Equals(icon, binding[..separator].Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var name = binding[(separator + 1)..].Trim();

        return [.. (GetIcon(icon)?.Parts ?? []).Where(p => p == name || GetKind(p) == name)];
    }

    public static int GetOrder(string icon, string part) => GetIcon(icon)?.Parts.IndexOf(part) ?? -1;

    public static Task<SvgImageSource?> GetOverlayAsync(string icon, string part, Color colour)
    {
        if (!Overlays.TryGetValue((icon, part, colour), out var overlay))
        {
            overlay = LoadOverlayAsync(icon, part, colour);

            Overlays[(icon, part, colour)] = overlay;
        }

        return overlay;
    }

    internal static string? CreateOverlay(XDocument icon, string part, string colour)
    {
        var document = new XDocument(icon);

        var shapes = document.Descendants().Where(e => Shapes.Contains(e.Name.LocalName)).ToList();

        var first = shapes.FindIndex(s => GetPartName(s) == part);

        if (first < 0)
        {
            return null;
        }

        for (var index = 0; index < shapes.Count; index++)
        {
            var shape = shapes[index];

            if (GetPartName(shape) == part)
            {
                Paint(shape, colour);
            }
            else if (index < first)
            {
                shape.Remove();
            }
        }

        return document.ToString(SaveOptions.DisableFormatting);
    }

    internal static List<string> GetParts(XDocument icon)
        => [.. icon.Descendants().Select(e => (string?)e.Attribute(PartAttribute)).OfType<string>().Distinct()];

    private static List<string> FindBindings()
    {
        var parts = new SortedSet<string>(StringComparer.Ordinal);

        var qualified = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(PlanIconResolver.IconFolder, "*.svg"))
        {
            var icon = Path.GetFileNameWithoutExtension(path);

            foreach (var part in (GetIcon(icon)?.Parts ?? []).SelectMany(p => GetKind(p) is { } kind ? [kind, p] : new[] { p }))
            {
                parts.Add(part);

                qualified.Add($"{GetDisplayName(icon)}{IconSeparator}{part}");
            }
        }

        return [.. parts, .. qualified];
    }

    private static string GetDisplayName(string icon) => WordBoundary().Replace(icon, " ");

    private static string? GetKind(string part) => NumberedPart().Match(part) is { Success: true } match ? match.Groups[1].Value : null;

    private static PlanIcon? GetIcon(string icon)
    {
        if (!Icons.TryGetValue(icon, out var loaded))
        {
            loaded = Load(PlanIconResolver.GetPath(icon));

            Icons[icon] = loaded;
        }

        return loaded;
    }

    private static PlanIcon? Load(string path)
    {
        try
        {
            var document = XDocument.Load(path);

            return new PlanIcon(document, GetParts(document));
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<SvgImageSource?> LoadOverlayAsync(string icon, string part, Color colour)
    {
        var hex = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";

        if (GetIcon(icon) is not { } loaded || CreateOverlay(loaded.Document, part, hex) is not { } svg)
        {
            return null;
        }

        using var stream = new InMemoryRandomAccessStream();

        await stream.WriteAsync(Encoding.UTF8.GetBytes(svg).AsBuffer());

        stream.Seek(0);

        var source = new SvgImageSource();

        return await source.SetSourceAsync(stream) == SvgImageSourceLoadStatus.Success ? source : null;
    }

    private static string? GetPartName(XElement element)
        => element.AncestorsAndSelf().Select(e => (string?)e.Attribute(PartAttribute)).FirstOrDefault(p => p is not null);

    private static string? GetInherited(XElement element, string name)
        => element.AncestorsAndSelf().Select(e => (string?)e.Attribute(name)).FirstOrDefault(v => v is not null);

    private static void Paint(XElement shape, string colour)
    {
        foreach (var element in shape.AncestorsAndSelf())
        {
            element.Attribute(DisplayAttribute)?.Remove();
        }

        if (GetInherited(shape, FillAttribute) != None)
        {
            shape.SetAttributeValue(FillAttribute, colour);
        }

        if (GetInherited(shape, StrokeAttribute) is { } stroke && stroke != None)
        {
            shape.SetAttributeValue(StrokeAttribute, colour);
        }
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();

    [GeneratedRegex(@"^(.+) \d+$")]
    private static partial Regex NumberedPart();

    private sealed record PlanIcon(XDocument Document, List<string> Parts);
}
