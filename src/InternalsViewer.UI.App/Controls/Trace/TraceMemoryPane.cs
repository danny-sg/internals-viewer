using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed class TraceMemoryPane(ITraceTimelineHost host, Func<(float Top, float Height)> getBounds) : IDisposable
{
    private const float BlockGap = 1f;

    private const float MinimumRunHeight = 1f;

    private const float MaximumBytePixels = 6f;

    private const float HitWidth = 4f;

    private const float HitHeight = 3f;

    private const string ToolTipTitle = "Memory";

    private static readonly SKColor PaneColour = new(30, 30, 30, 220);

    private static readonly SKColor LifetimeColour = new(90, 90, 90, 120);

    private static readonly SKColor HoverColour = new(255, 255, 255, 230);

    public IReadOnlyList<TraceMemoryBlock> Blocks { get; private set; } = [];

    public bool HasBlocks => Blocks.Count > 0;

    public TraceMemoryHit? Hover { get; set; }

    private ITraceTimelineHost Host { get; } = host;

    private Func<(float Top, float Height)> GetBounds { get; } = getBounds;

    private TraceScale TraceScale { get; set; } = TraceScale.Default;

    private TraceTimelinePaints Paints => Host.Paints;

    private float Top => GetBounds().Top;

    private float Height => GetBounds().Height;

    private Layout? CachedLayout { get; set; }

    private AxisIndex? CachedIndex { get; set; }

    private List<(int X, int Y, SKColor Colour)> Marks { get; } = [];

    private Dictionary<int, SKColor> Colours { get; } = [];

    private int[] Stamps { get; set; } = [];

    private int Stamp { get; set; }

    private SKImage? PointImage { get; set; }

    private SKImage PointSprite => PointImage ??= CreatePointSprite();

    private SKPaint PointPaint { get; } = new();

    public void SetBlocks(IReadOnlyList<TraceMemoryBlock>? blocks)
    {
        Blocks = blocks is null ? [] : [.. blocks.OrderBy(b => b.Address)];

        CachedLayout = null;

        CachedIndex = null;

        Hover = null;
    }

    public void SetScale(TraceScale scale)
    {
        TraceScale = scale;

        CachedIndex = null;
    }

    public bool Contains(Point position) => HasBlocks && position.Y >= Top && position.Y < Top + Height;

    public void Draw(SKCanvas canvas, int width, double scale)
    {
        Paints.Fill.Color = PaneColour;

        canvas.DrawRect(0, Top, width, Height, Paints.Fill);

        var layout = GetLayout();

        var index = GetIndex();

        canvas.Save();

        canvas.ClipRect(new SKRect(0, Top, width, Top + Height));

        PrepareMarks(width);

        for (var block = 0; block < Blocks.Count; block++)
        {
            DrawLifetime(canvas, block, layout, width, scale);

            AddMarks(block, layout, index, width, scale);
        }

        DrawPoints(canvas, Marks);

        canvas.Restore();
    }

    public void DrawOverlay(SKCanvas canvas, int width, double scale)
    {
        if (Hover is not { } hit || hit.Block >= Blocks.Count)
        {
            return;
        }

        var layout = GetLayout();

        var block = Blocks[hit.Block];

        canvas.Save();

        canvas.ClipRect(new SKRect(0, Top, width, Top + Height));

        if (hit.Write >= 0)
        {
            var (top, bottom) = layout.WriteRows[hit.Block][hit.Write];

            List<(int X, int Y, SKColor Colour)> mark = [((int)MathF.Floor(GetX(block.Writes[hit.Write].Point, scale)),
                                                         (int)MathF.Floor(Top + (top + bottom) / 2),
                                                         HoverColour)];

            DrawPoints(canvas, mark);
        }
        else
        {
            var left = Math.Max(GetX(block.From, scale), -1f);

            var right = block.To is { } to ? Math.Min(GetX(to, scale), width + 1f) : width + 1f;

            var (top, bottom) = layout.BlockRows[hit.Block];

            Paints.Outline.Color = HoverColour;

            Paints.Outline.StrokeWidth = 1f;

            canvas.DrawRect(new SKRect(left, Top + top, right, Top + bottom), Paints.Outline);
        }

        canvas.Restore();
    }

    public TraceMemoryHit? GetHit(Point position)
    {
        if (!Contains(position))
        {
            return null;
        }

        var layout = GetLayout();

        var y = (float)(position.Y - Top);

        var scale = Host.Scale;

        if (GetNearestWrite(layout, GetIndex(), position.X, y, scale) is { } write)
        {
            return write;
        }

        if (layout.GetSegmentAt(y) is not { } segment || layout.Segments[segment].GetAddressAt(y) is not { } address)
        {
            return null;
        }

        var value = Host.ViewStart + position.X / scale;

        var tolerance = HitWidth / scale;

        for (var index = 0; index < Blocks.Count; index++)
        {
            var block = Blocks[index];

            if (layout.BlockSegments[index] != segment || address < block.Address || address >= block.Address + (ulong)block.Size)
            {
                continue;
            }

            var from = TraceScale.GetValue(block.From, Host.Axis);

            var to = block.To is { } end ? TraceScale.GetValue(end, Host.Axis) : double.MaxValue;

            if (value >= from - tolerance && value <= to + tolerance)
            {
                return new TraceMemoryHit(index, -1, (int)(address - block.Address));
            }
        }

        return null;
    }

    public string? GetToolTip(TraceMemoryHit? hit, Point position)
    {
        if (hit is not { } found || found.Block >= Blocks.Count)
        {
            return Contains(position) ? ToolTipTitle : null;
        }

        var block = Blocks[found.Block];

        var lines = new List<string> { block.Label, $"0x{block.Address:X16} +0x{found.Offset:X}, {block.Size} Bytes" };

        if (found.Write >= 0)
        {
            lines.Add(Host.GetLabel(block.Writes[found.Write].CallNodeIndex));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public void Click(Point position)
    {
        if (GetHit(position) is not { } hit)
        {
            return;
        }

        var block = Blocks[hit.Block];

        Host.SelectMemoryWrite(block.Block, hit.Write >= 0 ? block.Writes[hit.Write].Point.Position : null);
    }

    public void Dispose()
    {
        PointImage?.Dispose();

        PointPaint.Dispose();
    }

    private static SKImage CreatePointSprite()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul));

        bitmap.SetPixel(0, 0, SKColors.White);

        return SKImage.FromPixelCopy(bitmap.PeekPixels());
    }

    private void DrawPoints(SKCanvas canvas, List<(int X, int Y, SKColor Colour)> marks)
    {
        if (marks.Count == 0)
        {
            return;
        }

        var bounds = new SKRect(0, 0, 1, 1);

        var sprites = new SKRect[marks.Count];

        var transforms = new SKRotationScaleMatrix[marks.Count];

        var colours = new SKColor[marks.Count];

        for (var index = 0; index < marks.Count; index++)
        {
            var (x, y, colour) = marks[index];

            sprites[index] = bounds;

            transforms[index] = SKRotationScaleMatrix.Create(1, 0, x, y, 0, 0);

            colours[index] = colour;
        }

        canvas.DrawAtlas(PointSprite, sprites, transforms, colours, SKBlendMode.Modulate, PointPaint);
    }

    private void PrepareMarks(int width)
    {
        Marks.Clear();

        Colours.Clear();

        var cells = Math.Max(width, 1) * ((int)MathF.Ceiling(Height) + 1);

        if (Stamps.Length < cells)
        {
            Stamps = new int[cells];

            Stamp = 0;
        }

        Stamp++;
    }

    private void DrawLifetime(SKCanvas canvas, int index, Layout layout, int width, double scale)
    {
        var block = Blocks[index];

        var left = GetX(block.From, scale);

        var right = block.To is { } to ? GetX(to, scale) : width;

        var (top, bottom) = layout.BlockRows[index];

        if (right < 0 || left > width || bottom <= top)
        {
            return;
        }

        var barLeft = Math.Max(left, -1f);

        Paints.Fill.Color = LifetimeColour;

        canvas.DrawRect(barLeft, Top + top, Math.Max(1f, Math.Min(right, width + 1f) - barLeft), bottom - top, Paints.Fill);
    }

    private void AddMarks(int block, Layout layout, AxisIndex index, int width, double scale)
    {
        var values = index.Values[block];

        var order = index.Order[block];

        var rows = layout.WriteRows[block];

        var writes = Blocks[block].Writes;

        var margin = 1 / scale;

        var end = Host.ViewEnd + margin;

        var paneTop = (int)MathF.Floor(Top);

        for (var position = GetLowerBound(values, Host.ViewStart - margin); position < values.Length && values[position] <= end; position++)
        {
            var write = order[position];

            var x = (int)Math.Floor((values[position] - Host.ViewStart) * scale);

            var y = (int)MathF.Floor(Top + (rows[write].Top + rows[write].Bottom) / 2);

            if (x < 0 || x >= width || y < paneTop)
            {
                continue;
            }

            var cell = (y - paneTop) * width + x;

            if (cell < Stamps.Length)
            {
                if (Stamps[cell] == Stamp)
                {
                    continue;
                }

                Stamps[cell] = Stamp;
            }

            Marks.Add((x, y, GetColour(writes[write].CallNodeIndex)));
        }
    }

    private SKColor GetColour(int callNodeIndex)
    {
        if (!Colours.TryGetValue(callNodeIndex, out var colour))
        {
            colour = Host.GetColour(callNodeIndex);

            Colours[callNodeIndex] = colour;
        }

        return colour;
    }

    private TraceMemoryHit? GetNearestWrite(Layout layout, AxisIndex index, double x, float y, double scale)
    {
        TraceMemoryHit? found = null;

        var nearest = double.MaxValue;

        var value = Host.ViewStart + x / scale;

        var tolerance = HitWidth / scale;

        for (var block = 0; block < Blocks.Count; block++)
        {
            var (blockTop, blockBottom) = layout.BlockRows[block];

            if (y < blockTop - HitHeight || y > blockBottom + HitHeight)
            {
                continue;
            }

            var values = index.Values[block];

            var rows = layout.WriteRows[block];

            for (var position = GetLowerBound(values, value - tolerance);
                 position < values.Length && values[position] <= value + tolerance;
                 position++)
            {
                var write = index.Order[block][position];

                var dx = Math.Abs((values[position] - value) * scale);

                var dy = Math.Abs((rows[write].Top + rows[write].Bottom) / 2 - y);

                if (dy > HitHeight + (rows[write].Bottom - rows[write].Top) / 2)
                {
                    continue;
                }

                var distance = dx * dx + dy * dy;

                if (distance < nearest)
                {
                    nearest = distance;

                    found = new TraceMemoryHit(block, write, Blocks[block].Writes[write].Offset);
                }
            }
        }

        return found;
    }

    private static int GetLowerBound(double[] values, double value)
    {
        var low = 0;

        var high = values.Length;

        while (low < high)
        {
            var middle = (low + high) / 2;

            if (values[middle] < value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private float GetX(TracePoint point, double scale) => (float)((TraceScale.GetValue(point, Host.Axis) - Host.ViewStart) * scale);

    private AxisIndex GetIndex()
    {
        if (CachedIndex is { } cached && cached.Axis == Host.Axis)
        {
            return cached;
        }

        var values = new double[Blocks.Count][];

        var order = new int[Blocks.Count][];

        for (var block = 0; block < Blocks.Count; block++)
        {
            var writes = Blocks[block].Writes;

            values[block] = [.. writes.Select(w => TraceScale.GetValue(w.Point, Host.Axis))];

            order[block] = [.. Enumerable.Range(0, writes.Length)];

            Array.Sort(values[block], order[block]);
        }

        return CachedIndex = new AxisIndex(Host.Axis, values, order);
    }

    private Layout GetLayout()
    {
        var height = Height;

        if (CachedLayout is { } cached && cached.Height == height)
        {
            return cached;
        }

        var segments = new List<(ulong Start, ulong End, List<(ulong Start, ulong End)> Runs)>();

        var blockSegments = new int[Blocks.Count];

        for (var index = 0; index < Blocks.Count; index++)
        {
            var block = Blocks[index];

            var end = block.Address + (ulong)block.Size;

            if (segments.Count == 0 || segments[^1].End <= block.Address)
            {
                segments.Add((block.Address, end, []));
            }
            else if (end > segments[^1].End)
            {
                segments[^1] = segments[^1] with { End = end };
            }

            blockSegments[index] = segments.Count - 1;

            segments[^1].Runs.AddRange(block.Writes.Where(w => w.Length > 0)
                                            .Select(w => (block.Address + (ulong)w.Offset, block.Address + (ulong)(w.Offset + w.Length))));
        }

        var runs = segments.Select(s => Merge(s.Runs)).ToList();

        var heights = GetRunHeights([.. runs.SelectMany(r => r).Select(r => r.End - r.Start)],
                                    height - BlockGap * Math.Max(segments.Count - 1, 0));

        var built = new Segment[segments.Count];

        var top = 0f;

        var next = 0;

        for (var index = 0; index < segments.Count; index++)
        {
            var placed = new List<Run>();

            var y = top;

            foreach (var (start, end) in runs[index])
            {
                placed.Add(new Run(start, end, y, heights[next] / (end - start)));

                y += heights[next++];
            }

            built[index] = new Segment(top, y, [.. placed]);

            top = y + BlockGap;
        }

        var blockRows = new (float Top, float Bottom)[Blocks.Count];

        var writeRows = new (float Top, float Bottom)[Blocks.Count][];

        for (var index = 0; index < Blocks.Count; index++)
        {
            var block = Blocks[index];

            var segment = built[blockSegments[index]];

            blockRows[index] = (segment.GetY(block.Address), segment.GetY(block.Address + (ulong)block.Size));

            writeRows[index] = [.. block.Writes.Select(w => (segment.GetY(block.Address + (ulong)w.Offset),
                                                             segment.GetY(block.Address + (ulong)(w.Offset + w.Length))))];
        }

        return CachedLayout = new Layout(height, built, blockSegments, blockRows, writeRows);
    }

    private static List<(ulong Start, ulong End)> Merge(List<(ulong Start, ulong End)> runs)
    {
        var merged = new List<(ulong Start, ulong End)>();

        foreach (var run in runs.OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && merged[^1].End >= run.Start)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, run.End));

                continue;
            }

            merged.Add(run);
        }

        return merged;
    }

    private static float[] GetRunHeights(List<ulong> lengths, float height)
    {
        var heights = new float[lengths.Count];

        if (lengths.Count == 0)
        {
            return heights;
        }

        var minimum = Math.Min(MinimumRunHeight, Math.Max(height, 0) / lengths.Count);

        var isMinimum = new bool[lengths.Count];

        while (true)
        {
            var room = (double)height;

            var bytes = 0d;

            for (var index = 0; index < lengths.Count; index++)
            {
                if (isMinimum[index])
                {
                    room -= minimum;
                }
                else
                {
                    bytes += lengths[index];
                }
            }

            var perByte = bytes > 0 ? Math.Min(Math.Max(room, 0) / bytes, MaximumBytePixels) : 0;

            var changed = false;

            for (var index = 0; index < lengths.Count; index++)
            {
                if (!isMinimum[index] && lengths[index] * perByte < minimum)
                {
                    isMinimum[index] = true;

                    changed = true;
                }
            }

            if (changed)
            {
                continue;
            }

            for (var index = 0; index < lengths.Count; index++)
            {
                heights[index] = isMinimum[index] ? minimum : (float)(lengths[index] * perByte);
            }

            return heights;
        }
    }

    private sealed record AxisIndex(TraceTimelineAxis Axis, double[][] Values, int[][] Order);

    private sealed record Run(ulong Start, ulong End, float Top, float BytePixels);

    private sealed record Segment(float Top, float Bottom, Run[] Runs)
    {
        public float GetY(ulong address)
        {
            var low = 0;

            var high = Runs.Length;

            while (low < high)
            {
                var middle = (low + high) / 2;

                if (Runs[middle].End <= address)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            if (low == Runs.Length)
            {
                return Bottom;
            }

            var run = Runs[low];

            return address < run.Start ? run.Top : run.Top + (address - run.Start) * run.BytePixels;
        }

        public ulong? GetAddressAt(float y)
        {
            var low = 0;

            var high = Runs.Length;

            while (low < high)
            {
                var middle = (low + high) / 2;

                if (Runs[middle].Top <= y)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            if (low == 0)
            {
                return null;
            }

            var run = Runs[low - 1];

            var bottom = run.Top + (run.End - run.Start) * run.BytePixels;

            return y < bottom ? run.Start + Math.Min((ulong)((y - run.Top) / run.BytePixels), run.End - run.Start - 1) : null;
        }
    }

    private sealed record Layout(float Height,
                                 Segment[] Segments,
                                 int[] BlockSegments,
                                 (float Top, float Bottom)[] BlockRows,
                                 (float Top, float Bottom)[][] WriteRows)
    {
        public int? GetSegmentAt(float y)
        {
            for (var index = 0; index < Segments.Length; index++)
            {
                if (y >= Segments[index].Top && y < Segments[index].Bottom)
                {
                    return index;
                }
            }

            return null;
        }
    }
}
