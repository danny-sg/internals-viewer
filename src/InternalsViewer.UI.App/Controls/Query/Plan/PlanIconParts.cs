using System;
using System.Collections.Generic;
using System.Globalization;
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
    public const string PageIcon = "Page";

    public const string ActivePart = "Active";

    private const string PartAttribute = "data-part";

    private const string PageAttribute = "data-page";

    private const string DisplayAttribute = "display";

    private const string RangeAttribute = "data-range";

    private const string ViewBoxAttribute = "viewBox";

    private const string ComputeScalarIcon = "ComputeScalar";

    private const string CalculatorPart = "Calculator";

    private const string CalculatorViewBox = "15 12 14 17";

    private const string FillAttribute = "fill";

    private const string StrokeAttribute = "stroke";

    private const string StyleAttribute = "style";

    private const double InactiveLightening = 0.5;

    private const string None = "none";

    private const char IconSeparator = '.';

    private static readonly HashSet<string> Shapes = ["path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text"];

    private static Dictionary<string, PlanIcon?> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<(string Icon, string Part, Color Colour, double? Fraction), string?> Overlays { get; } = [];

    private static Dictionary<string, string?> InactiveIcons { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string>? Bindings { get; set; }

    private static string? CalculatorIcon { get; set; }

    private static string? InactiveCalculatorIcon { get; set; }

    public static IReadOnlyList<string> GetBindings() => Bindings ??= FindBindings();

    public static bool HasPage(string icon) => GetIcon(icon)?.HasPage == true;

    public static IReadOnlyList<string> GetParts(string icon, string binding, string? owner = null)
    {
        var separator = binding.LastIndexOf(IconSeparator);

        var operatorIcon = owner ?? icon;

        if (separator >= 0
            && !string.Equals(operatorIcon, binding[..separator].Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var name = binding[(separator + 1)..].Trim();

        if (name == ActivePart)
        {
            return string.Equals(icon, PageIcon, StringComparison.OrdinalIgnoreCase) ? [] : [ActivePart];
        }

        return [.. (GetIcon(icon)?.Parts ?? []).Where(p => p == name || GetKind(p) == name)];
    }

    public static bool IsActiveBinding(string? binding)
        => binding is { Length: > 0 } && binding[(binding.LastIndexOf(IconSeparator) + 1)..].Trim() == ActivePart;

    public static string? GetCalculatorIcon(bool isInactive)
    {
        CalculatorIcon ??= GetIcon(ComputeScalarIcon) is { } loaded
            ? CreatePartIcon(loaded.Document, CalculatorPart, CalculatorViewBox)
            : null;

        if (!isInactive || CalculatorIcon is null)
        {
            return CalculatorIcon;
        }

        return InactiveCalculatorIcon ??= CreateInactive(XDocument.Parse(CalculatorIcon));
    }

    public static string? GetInactiveIcon(string icon)
    {
        if (!InactiveIcons.TryGetValue(icon, out var svg))
        {
            svg = GetIcon(icon) is { } loaded ? CreateInactive(loaded.Document) : null;

            InactiveIcons[icon] = svg;
        }

        return svg;
    }

    public static int GetOrder(string icon, string part) => GetIcon(icon)?.Parts.IndexOf(part) ?? -1;

    public static string? GetOverlay(string icon, string part, Color colour, double? fraction)
    {
        if (!Overlays.TryGetValue((icon, part, colour, fraction), out var overlay))
        {
            var hex = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";

            overlay = GetIcon(icon) is not { } loaded ? null
                      : part == ActivePart ? loaded.Document.ToString(SaveOptions.DisableFormatting)
                      : CreateOverlay(loaded.Document, part, hex, fraction);

            Overlays[(icon, part, colour, fraction)] = overlay;
        }

        return overlay;
    }

    public static SvgImageSource CreateCalculatorSource(int pixelWidth, int pixelHeight)
    {
        var source = new SvgImageSource { RasterizePixelWidth = pixelWidth, RasterizePixelHeight = pixelHeight };

        if (GetCalculatorIcon(false) is { } svg)
        {
            _ = LoadAsync(source, svg);
        }

        return source;
    }

    public static async Task LoadAsync(SvgImageSource source, string svg)
    {
        using var stream = new InMemoryRandomAccessStream();

        await stream.WriteAsync(Encoding.UTF8.GetBytes(svg).AsBuffer());

        stream.Seek(0);

        await source.SetSourceAsync(stream);
    }

    internal static string? CreateOverlay(XDocument icon, string part, string colour, double? fraction = null)
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

                if (fraction is { } value)
                {
                    Place(shape, value);
                }
            }
            else if (index < first)
            {
                shape.Remove();
            }
        }

        return document.ToString(SaveOptions.DisableFormatting);
    }

    internal static string CreatePartIcon(XDocument icon, string part, string viewBox)
    {
        var document = new XDocument(icon);

        foreach (var shape in document.Descendants().Where(e => Shapes.Contains(e.Name.LocalName) && GetPartName(e) != part).ToList())
        {
            shape.Remove();
        }

        document.Root?.SetAttributeValue(ViewBoxAttribute, viewBox);

        return document.ToString(SaveOptions.DisableFormatting);
    }

    internal static string CreateInactive(XDocument icon)
    {
        var document = new XDocument(icon);

        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name.LocalName is FillAttribute or StrokeAttribute && ToInactive(attribute.Value) is { } grey)
                {
                    attribute.Value = grey;
                }
                else if (attribute.Name.LocalName == StyleAttribute)
                {
                    attribute.Value = StyleColour().Replace(attribute.Value,
                                                            m => m.Groups[1].Value + (ToInactive(m.Groups[2].Value) ?? m.Groups[2].Value));
                }
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

        var pageParts = GetIcon(PageIcon)?.Parts ?? [];

        foreach (var path in Directory.EnumerateFiles(PlanIconResolver.IconFolder, "*.svg"))
        {
            var icon = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(icon, PageIcon, StringComparison.OrdinalIgnoreCase) || GetIcon(icon) is not { } loaded)
            {
                continue;
            }

            List<string> iconParts = [ActivePart, .. loaded.Parts, .. loaded.HasPage ? pageParts : []];

            foreach (var part in iconParts.SelectMany(p => GetKind(p) is { } kind ? [kind, p] : new[] { p }))
            {
                parts.Add(part);

                qualified.Add($"{GetDisplayName(icon)}{IconSeparator}{part}");
            }
        }

        return [.. parts, .. qualified];
    }

    private static string GetDisplayName(string icon) => WordBoundary().Replace(icon, " ");

    private static void Place(XElement shape, double fraction)
    {
        if (((string?)shape.Attribute(RangeAttribute))?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is not [var low, var high]
            || !double.TryParse(low, NumberStyles.Float, CultureInfo.InvariantCulture, out var top)
            || !double.TryParse(high, NumberStyles.Float, CultureInfo.InvariantCulture, out var bottom))
        {
            return;
        }

        var y = (top + Math.Round(Math.Clamp(fraction, 0, 1) * (bottom - top))).ToString(CultureInfo.InvariantCulture);

        if (shape.Name.LocalName == "line")
        {
            shape.SetAttributeValue("y1", y);

            shape.SetAttributeValue("y2", y);
        }
        else
        {
            shape.SetAttributeValue("y", y);
        }
    }

    private static string? ToInactive(string colour)
    {
        if (!TryParseColour(colour, out var red, out var green, out var blue))
        {
            return null;
        }

        var luma = 0.299 * red + 0.587 * green + 0.114 * blue;

        var grey = (int)Math.Round(luma + (byte.MaxValue - luma) * InactiveLightening);

        return $"#{grey:X2}{grey:X2}{grey:X2}";
    }

    private static bool TryParseColour(string colour, out int red, out int green, out int blue)
    {
        var value = colour.Trim().ToLowerInvariant() switch
        {
            "white" => "#ffffff",
            "black" => "#000000",
            var other => other
        };

        if (value.Length == 4 && value[0] == '#')
        {
            value = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}";
        }

        if (value.Length != 7
            || value[0] != '#'
            || !int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            (red, green, blue) = (0, 0, 0);

            return false;
        }

        (red, green, blue) = ((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        return true;
    }

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

            return new PlanIcon(document, GetParts(document), (string?)document.Root?.Attribute(PageAttribute) == "true");
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            return null;
        }
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

    [GeneratedRegex(@"((?:fill|stroke)\s*:\s*)(#[0-9a-fA-F]{3,6}|white|black)")]
    private static partial Regex StyleColour();

    private sealed record PlanIcon(XDocument Document, List<string> Parts, bool HasPage);
}
