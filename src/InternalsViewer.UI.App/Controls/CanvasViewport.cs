using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// The zoom and pan a canvas control draws its content through
/// </summary>
/// <remarks>
/// Content is laid out at its natural size and the whole drawing scaled, so a control keeps the layout it has at a zoom
/// of one. Offsets are held in canvas pixels, which is what the scroll bars carry, and are clamped to the extents the
/// content has at the current zoom - at or below the zoom that fits, there is nothing to pan and the offset is zero.
/// </remarks>
internal sealed class CanvasViewport
{
    private const float MinimumZoom = 0.2f;

    private const float MaximumZoom = 8f;

    private const float ZoomStep = 1.1f;

    private const float ZoomEpsilon = 0.0001f;

    /// <summary>
    /// The zoom the wheel stops at on its way past, which is the one the content was laid out for
    /// </summary>
    private const float DetentZoom = 1f;

    // Wheel notches further apart than this (microseconds) are a new gesture, which the detent does not hold.
    private const ulong GestureGapUs = 350_000;

    private ulong _lastZoomTimestamp;

    private int _detentDirection;

    private float _contentWidth;

    private float _contentHeight;

    private float _viewportWidth;

    private float _viewportHeight;

    public float Zoom { get; private set; } = 1f;

    public float OffsetX { get; private set; }

    public float OffsetY { get; private set; }

    public float MaximumOffsetX { get; private set; }

    public float MaximumOffsetY { get; private set; }

    /// <summary>
    /// Scales and translates the canvas so the content can be drawn in its own coordinates
    /// </summary>
    public void Apply(SKCanvas canvas)
    {
        canvas.Translate(-OffsetX, -OffsetY);

        canvas.Scale(Zoom);
    }

    /// <summary>
    /// The point in content coordinates under a point on the canvas
    /// </summary>
    public SKPoint ToContent(double x, double y) => new((float)((x + OffsetX) / Zoom), (float)((y + OffsetY) / Zoom));

    /// <summary>
    /// Records the size of the content and of the canvas showing it, which is what the pan is bounded by
    /// </summary>
    public bool SetExtent(float contentWidth, float contentHeight, float viewportWidth, float viewportHeight)
    {
        _contentWidth = contentWidth;
        _contentHeight = contentHeight;
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;

        return UpdateExtent();
    }

    public bool SetOffset(float x, float y)
    {
        var clampedX = Math.Clamp(x, 0, MaximumOffsetX);
        var clampedY = Math.Clamp(y, 0, MaximumOffsetY);

        if (Math.Abs(clampedX - OffsetX) < 0.5f && Math.Abs(clampedY - OffsetY) < 0.5f)
        {
            return false;
        }

        OffsetX = clampedX;
        OffsetY = clampedY;

        return true;
    }

    /// <summary>
    /// Zooms about a point on the canvas, so whatever is under the pointer stays under it
    /// </summary>
    /// <remarks>
    /// A step that would cross a zoom of one stops there instead, and the wheel is held at it for the rest of the
    /// gesture: turning it on past the stop takes a fresh one, either by letting the wheel rest or by releasing the
    /// modifier and taking hold again. Turning back the way it came is not held, so a stop can be backed out of.
    /// </remarks>
    public bool ZoomAt(int wheelDelta, double x, double y, ulong timestamp)
    {
        var direction = Math.Sign(wheelDelta);

        var isSameGesture = _lastZoomTimestamp > 0 && timestamp >= _lastZoomTimestamp
                            && timestamp - _lastZoomTimestamp <= GestureGapUs;

        _lastZoomTimestamp = timestamp;

        if (_detentDirection != 0 && isSameGesture && direction == _detentDirection)
        {
            return false;
        }

        _detentDirection = 0;

        var zoom = Math.Clamp(Zoom * (wheelDelta > 0 ? ZoomStep : 1f / ZoomStep), MinimumZoom, MaximumZoom);

        if ((Zoom > DetentZoom && zoom < DetentZoom) || (Zoom < DetentZoom && zoom > DetentZoom))
        {
            zoom = DetentZoom;

            _detentDirection = direction;
        }

        if (Math.Abs(zoom - Zoom) < ZoomEpsilon)
        {
            return false;
        }

        var anchor = ToContent(x, y);

        Zoom = zoom;

        UpdateExtent();

        OffsetX = Math.Clamp(anchor.X * Zoom - (float)x, 0, MaximumOffsetX);
        OffsetY = Math.Clamp(anchor.Y * Zoom - (float)y, 0, MaximumOffsetY);

        return true;
    }

    private bool UpdateExtent()
    {
        MaximumOffsetX = Math.Max(0, _contentWidth * Zoom - _viewportWidth);
        MaximumOffsetY = Math.Max(0, _contentHeight * Zoom - _viewportHeight);

        return SetOffset(OffsetX, OffsetY);
    }
}
