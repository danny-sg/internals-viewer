using System.Globalization;

namespace InternalsViewer.Internals.Helpers;

public static class NumberHelpers
{
    private const string SignificantDigits = "G15";

    public static decimal ToDecimal(double value)
        => decimal.Parse(value.ToString(SignificantDigits, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
}
