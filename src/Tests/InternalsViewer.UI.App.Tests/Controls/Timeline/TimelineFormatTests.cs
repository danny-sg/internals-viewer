using System.Globalization;
using InternalsViewer.UI.App.Controls.Timeline;

namespace InternalsViewer.UI.App.Tests.Controls.Timeline;

[Trait("Category", "Unit")]
[Trait("Area", "Timeline")]
public class TimelineFormatTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(0.3, 0.5)]
    [InlineData(1, 1)]
    [InlineData(1.5, 2)]
    [InlineData(3, 5)]
    [InlineData(7, 10)]
    [InlineData(12, 20)]
    [InlineData(80, 100)]
    [InlineData(100, 100)]
    public void Rounds_An_Interval_Up_To_The_Nearest_Nice_Value(double raw, double expected)
    {
        Assert.Equal(expected, TimelineFormat.NiceInterval(raw), precision: 10);
    }

    [Theory]
    [InlineData(0, "0ms")]
    [InlineData(-5, "0ms")]
    [InlineData(0.5, "0.5ms")]
    [InlineData(5.25, "5.25ms")]
    [InlineData(42, "42ms")]
    [InlineData(999.4, "999ms")]
    [InlineData(1500, "1.50s")]
    [InlineData(15000, "15.0s")]
    public void Formats_A_Time_Value_With_Its_Unit(double ms, string expected)
    {
        Assert.Equal(expected, Format(ms));
    }

    private static string Format(double ms)
    {
        var culture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            Span<char> buffer = stackalloc char[12];

            var length = TimelineFormat.FormatTimeIntoSpan(ms, buffer);

            return new string(buffer[..length]);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
