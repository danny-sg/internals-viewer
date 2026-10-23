using System;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    private const double MarkerMapWidth = 3;

    private const double MarkerMapMinimumHeight = 2;

    private const byte MarkerMapAlpha = 128;

    private void DrawMarkerMap()
    {
        MarkerMapCanvas.Children.Clear();

        var length = IsVirtualized ? TotalLength : Data?.Length ?? 0;

        var height = MarkerMapCanvas.ActualHeight;

        if (Markers is not { Count: > 0 } markers || length <= 0 || height <= 0)
        {
            return;
        }

        var left = Math.Floor((MarkerMapCanvas.ActualWidth - MarkerMapWidth) / 2);

        var (visibleStart, visibleEnd) = GetVisibleRange();

        foreach (var marker in markers)
        {
            if (!marker.IsVisible || marker.StartPosition < 0 || marker.EndPosition < marker.StartPosition)
            {
                continue;
            }

            if (marker.StartPosition < visibleEnd && marker.EndPosition >= visibleStart)
            {
                continue;
            }

            var top = Math.Floor(marker.StartPosition * height / length);

            var bottom = Math.Max(top + MarkerMapMinimumHeight, Math.Ceiling((marker.EndPosition + 1) * height / length));

            var mark = new Rectangle
            {
                Width = MarkerMapWidth,
                Height = Math.Min(bottom, height) - top,
                Fill = new SolidColorBrush(GetMapColour(marker))
            };

            Canvas.SetLeft(mark, left);

            Canvas.SetTop(mark, top);

            MarkerMapCanvas.Children.Add(mark);
        }
    }

    private static Color GetMapColour(Marker marker)
    {
        var colour = marker.MapColour ?? marker.BackColour;

        return Color.FromArgb(MarkerMapAlpha, colour.R, colour.G, colour.B);
    }

    private (int Start, int End) GetVisibleRange()
    {
        if (IsVirtualized)
        {
            return (WindowOffset, WindowOffset + WindowLength);
        }

        var lineHeight = GetLineHeight();

        if (lineHeight <= 0)
        {
            return (0, int.MaxValue);
        }

        var firstLine = (int)Math.Floor(ScrollViewer.VerticalOffset / lineHeight);

        var lineCount = (int)Math.Ceiling(ScrollViewer.ViewportHeight / lineHeight);

        return (firstLine * BytesPerLine, (firstLine + lineCount) * BytesPerLine);
    }
}
