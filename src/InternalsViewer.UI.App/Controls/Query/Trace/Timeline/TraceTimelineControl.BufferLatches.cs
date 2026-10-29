using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Memory;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float BufferLatchHeight = 6f;

    private const float BufferLatchDividerHeight = 1f;

    private const int BufferLatchMarkDensity = 4;

    private const int LargeBufferLatchMark = 2;

    private const float BufferLatchHitWidth = 4f;

    public static readonly DependencyProperty BufferLatchesProperty =
        DependencyProperty.Register(nameof(BufferLatches),
                                    typeof(TraceBufferLatch[]),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnBufferLatchesChanged));

    public TraceBufferLatch[]? BufferLatches
    {
        get => (TraceBufferLatch[]?)GetValue(BufferLatchesProperty);
        set => SetValue(BufferLatchesProperty, value);
    }

    public static readonly DependencyProperty ShowBufferLatchesProperty =
        DependencyProperty.Register(nameof(ShowBufferLatches),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(true, OnBufferLatchesChanged));

    public bool ShowBufferLatches
    {
        get => (bool)GetValue(ShowBufferLatchesProperty);
        set => SetValue(ShowBufferLatchesProperty, value);
    }

    public static readonly DependencyProperty ShowBuffersProperty =
        DependencyProperty.Register(nameof(ShowBuffers),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(false, OnShowBuffersChanged));

    public bool ShowBuffers
    {
        get => (bool)GetValue(ShowBuffersProperty);
        set => SetValue(ShowBuffersProperty, value);
    }

    private BufferLatchIndex? _bufferLatchIndex;

    private bool ShowsBufferLatches => ShowBufferLatches && ShowsMemory && BufferLatches is { Length: > 0 };

    private float BufferLatchArea => ShowsBufferLatches ? BufferLatchHeight + BufferLatchDividerHeight : 0;

    private float BufferLatchTop => MemoryTop + MemoryHeight - BufferLatchHeight;

    private void DrawBufferLatches(SKCanvas canvas, int width, double scale)
    {
        var top = BufferLatchTop;

        _paints.Fill.Color = TraceMemoryPane.PaneColour;

        canvas.DrawRect(0, top - BufferLatchDividerHeight, width, BufferLatchArea, _paints.Fill);

        var divider = top - BufferLatchDividerHeight / 2;

        canvas.DrawLine(0, divider, width, divider, _paints.Tick);

        if (BufferLatches is not { } latches)
        {
            return;
        }

        var lookup = GetBufferLatchIndex(latches);

        var first = SortedSearch.FirstAtOrAfter(lookup.Values, _viewStart);

        var last = SortedSearch.FirstAfter(lookup.Values, _viewEnd, first);

        var markSize = (last - first) / BufferLatchMarkDensity < width ? LargeBufferLatchMark : 1;

        var cells = new BufferLatchState?[width / markSize + 1];

        for (var index = first; index < last; index++)
        {
            var cell = (int)((lookup.Values[index] - _viewStart) * scale / markSize);

            var state = latches[lookup.Order[index]].State;

            if (cell >= 0 && cell < cells.Length && (cells[cell] is not { } shown || state > shown))
            {
                cells[cell] = state;
            }
        }

        for (var cell = 0; cell < cells.Length; cell++)
        {
            if (cells[cell] is { } state)
            {
                _paints.Fill.Color = GetBufferLatchColour(state);

                canvas.DrawRect(cell * markSize, top, markSize, BufferLatchHeight, _paints.Fill);
            }
        }
    }

    private TraceBufferLatch? GetBufferLatchAt(Point position)
    {
        if (!ShowsBufferLatches
            || BufferLatches is not { } latches
            || position.Y < BufferLatchTop
            || position.Y >= BufferLatchTop + BufferLatchHeight)
        {
            return null;
        }

        var lookup = GetBufferLatchIndex(latches);

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        var value = _viewStart + position.X / scale;

        var tolerance = BufferLatchHitWidth / scale;

        int? nearest = null;

        for (var index = SortedSearch.FirstAtOrAfter(lookup.Values, value - tolerance);
             index < lookup.Values.Length && lookup.Values[index] <= value + tolerance;
             index++)
        {
            if (nearest is not { } best || Math.Abs(lookup.Values[index] - value) < Math.Abs(lookup.Values[best] - value))
            {
                nearest = index;
            }
        }

        return nearest is { } hit ? latches[lookup.Order[hit]] : null;
    }

    private void OpenBufferLatch(TraceBufferLatch latch)
    {
        ITraceTimelineHost host = this;

        host.MovePlayhead(TraceScale.GetValue(latch.Point, _axis));

        host.ShowMemory(latch.Block, latch.Address, latch.Point.Position);
    }

    private ToggleMenuFlyoutItem LatchingItem()
    {
        var item = MenuItems.Toggle("Latching", ShowBufferLatches, isChecked => ShowBufferLatches = isChecked);

        item.IsEnabled = BufferLatches is { Length: > 0 };

        return item;
    }

    private ToggleMenuFlyoutItem BuffersItem()
    {
        var item = MenuItems.Toggle("BUF", ShowBuffers, isChecked => ShowBuffers = isChecked);

        item.IsEnabled = MemoryBlocks?.Any(b => b.IsBuffer) == true;

        return item;
    }

    private BufferLatchIndex GetBufferLatchIndex(TraceBufferLatch[] latches)
    {
        if (_bufferLatchIndex is { } cached
            && ReferenceEquals(cached.Latches, latches)
            && cached.Axis == _axis
            && ReferenceEquals(cached.Scale, TraceScale))
        {
            return cached;
        }

        var order = new List<int>(latches.Length);

        for (var index = 0; index < latches.Length; index++)
        {
            if (_axis == TraceTimelineAxis.Position || latches[index].HasInstructions)
            {
                order.Add(index);
            }
        }

        var values = new double[order.Count];

        for (var index = 0; index < order.Count; index++)
        {
            values[index] = TraceScale.GetValue(latches[order[index]].Point, _axis);
        }

        int[] sorted = [.. order];

        Array.Sort(values, sorted);

        _bufferLatchIndex = new BufferLatchIndex(latches, _axis, TraceScale, values, sorted);

        return _bufferLatchIndex;
    }

    private SKColor GetBufferLatchColour(BufferLatchState state) => state switch
    {
        BufferLatchState.Shared => _paints.LatchShared,
        BufferLatchState.Exclusive => _paints.LatchExclusive,
        BufferLatchState.Waiting => _paints.LatchWaiting,
        _ => _paints.LatchReleased
    };

    private static string GetBufferLatchToolTip(TraceBufferLatch latch)
    {
        var state = latch.State switch
        {
            BufferLatchState.Shared => "Shared Latch",
            BufferLatchState.Exclusive => "Exclusive Latch",
            BufferLatchState.Waiting => "Latch Waiters",
            _ => "Latch Released"
        };

        return $"{latch.Label}{Environment.NewLine}{state}";
    }

    private static void OnBufferLatchesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._bufferLatchIndex = null;

        control.HideToolTip();

        control.Relayout();
    }

    private static void OnShowBuffersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TraceTimelineControl)d).SetMemoryBlocks();

    private sealed record BufferLatchIndex(TraceBufferLatch[] Latches,
                                           TraceTimelineAxis Axis,
                                           TraceScale Scale,
                                           double[] Values,
                                           int[] Order);
}
