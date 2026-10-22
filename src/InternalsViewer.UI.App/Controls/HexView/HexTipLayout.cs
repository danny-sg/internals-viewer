using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.HexView;

public static class HexTipLayout
{
    public const double Gap = 2;

    public static Rect? GetAnchor(HexMetrics metrics,
                                  int anchor,
                                  int baseAddress,
                                  int length,
                                  double lineHeight,
                                  Rect viewport)
    {
        var position = anchor - baseAddress;

        if (position < 0 || position >= length)
        {
            return null;
        }

        var bounds = metrics.GetSpanRects(position, position, lineHeight)[0];

        var centre = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));

        if (!viewport.Contains(centre))
        {
            return null;
        }

        return new Rect(bounds.X - viewport.X, bounds.Y - viewport.Y, bounds.Width, bounds.Height);
    }

    public static Point GetTipPosition(Rect anchor) => new(anchor.Right + Gap, anchor.Bottom + Gap);

    public static Point GetValuePosition(Rect anchor, double offset) => new(anchor.Right + Gap + offset, anchor.Top);
}
