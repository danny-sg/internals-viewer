using System;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Time-axis formatting: choosing round tick intervals and rendering millisecond values into a span
/// </summary>
internal static class TimelineFormat
{
    /// <summary>Rounds a raw interval up to the nearest 1/2/5×10ⁿ so ruler ticks land on readable values</summary>
    public static double NiceInterval(double raw)
    {
        if (raw <= 0)
        {
            return 0;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));

        var fraction = raw / magnitude;

        var nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;

        return nice * magnitude;
    }

    /// <summary>Formats a millisecond value into <paramref name="buffer"/>, switching to seconds above 1000ms</summary>
    public static int FormatTimeIntoSpan(double ms, Span<char> buffer)
    {
        if (ms < 0)
        {
            ms = 0;
        }

        bool ok;
        int written;

        if (ms < 10)
        {
            ok = ms.TryFormat(buffer, out written, "0.##");
        }
        else if (ms < 1000)
        {
            ok = ms.TryFormat(buffer, out written, "0");
        }
        else
        {
            var seconds = ms / 1000.0;
            ok = seconds < 10
                ? seconds.TryFormat(buffer, out written, "0.00")
                : seconds.TryFormat(buffer, out written, "0.0");

            if (ok && written + 1 <= buffer.Length)
            {
                buffer[written++] = 's';
                return written;
            }

            return ok ? written : 0;
        }

        if (ok && written + 2 <= buffer.Length)
        {
            buffer[written++] = 'm';
            buffer[written++] = 's';
        }

        return ok ? written : 0;
    }
}
