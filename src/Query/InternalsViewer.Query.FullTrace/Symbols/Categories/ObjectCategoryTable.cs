using System.Globalization;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols.Categories;

public sealed class ObjectCategoryTable
{
    private const string ObjectsFile = "ObjectCategories.txt";

    private const string DefaultColour = "#808080";

    private const double LightnessStep = 0.07;

    private const double HueStep = 5;

    private const double MinimumLightness = 0.18;

    private const double MaximumLightness = 0.62;

    private const double FullCircle = 360;

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

            badges[(stage, name)] = new ObjectBadge(name, stage, GetShade(stage, index, counts[stage]));
        }

        return new ObjectCategoryTable([.. rows.Select(r => new ObjectCategoryRule(r.Class, badges[(r.Stage, r.Badge)]))]);
    }

    private static ObjectCategoryTable LoadDefault()
    {
        using var reader = CategoryRuleTable.OpenResource(typeof(ObjectCategoryTable).Assembly, ObjectsFile);

        return Load(reader);
    }

    private static string GetShade(ObjectStage stage, int index, int count)
    {
        var (hue, saturation, lightness) = ToHsl(stage.GetCategoryMetadata()?.ForegroundColor ?? DefaultColour);

        var step = (index + 1) / 2;

        var sign = index % 2 == 0 ? 1 : -1;

        var room = sign > 0 ? MaximumLightness - lightness : lightness - MinimumLightness;

        var steps = Math.Max(1, sign > 0 ? (count - 1) / 2 : count / 2);

        var lightnessStep = Math.Min(LightnessStep, Math.Max(0, room) / steps);

        var shadeHue = (hue + sign * step * HueStep + FullCircle) % FullCircle;

        var shadeLightness = Math.Clamp(lightness + sign * step * lightnessStep, MinimumLightness, MaximumLightness);

        return ToHex(shadeHue, saturation, shadeLightness);
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(string hex)
    {
        var red = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber) / 255.0;

        var green = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber) / 255.0;

        var blue = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber) / 255.0;

        var maximum = Math.Max(red, Math.Max(green, blue));

        var minimum = Math.Min(red, Math.Min(green, blue));

        var lightness = (maximum + minimum) / 2;

        var delta = maximum - minimum;

        if (delta == 0)
        {
            return (0, 0, lightness);
        }

        var saturation = delta / (1 - Math.Abs(2 * lightness - 1));

        var hue = maximum == red
            ? 60 * ((green - blue) / delta % 6)
            : maximum == green
                ? 60 * ((blue - red) / delta + 2)
                : 60 * ((red - green) / delta + 4);

        return ((hue + FullCircle) % FullCircle, saturation, lightness);
    }

    private static string ToHex(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;

        var second = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));

        var match = lightness - chroma / 2;

        var (red, green, blue) = (int)(hue / 60) switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second)
        };

        return $"#{ToByte(red + match):X2}{ToByte(green + match):X2}{ToByte(blue + match):X2}";
    }

    private static int ToByte(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255);

    private sealed record ObjectCategoryRule(GlobPattern Class, ObjectBadge Badge);
}
