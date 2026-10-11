using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Simulation.Steps;

public enum SimulationCounterKind
{
    Pair,
    Badge,
    Pill,
    Text
}

public sealed partial class SimulationCounter : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    public required string Name { get; init; }

    public SimulationCounterKind Kind { get; init; }

    public Color Colour { get; init; } = SimulationCounterColours.Neutral;

    public object? Value { get; private set; }

    public long Number => Value is long number ? number : 0;

    public void Update(object? value, string? format)
    {
        Value = value;

        Text = value switch
        {
            null => string.Empty,
            long number => number.ToString(format ?? "N0", System.Globalization.CultureInfo.CurrentCulture),
            int number => number.ToString(format ?? "N0", System.Globalization.CultureInfo.CurrentCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}

public static class SimulationCounterColours
{
    public static readonly Color Neutral = Color.FromArgb(255, 0x5A, 0x5A, 0x5A);

    public static readonly Color Success = Color.FromArgb(255, 0x2E, 0x7D, 0x32);

    public static readonly Color Caution = Color.FromArgb(255, 0xC2, 0x8A, 0x1E);
}
