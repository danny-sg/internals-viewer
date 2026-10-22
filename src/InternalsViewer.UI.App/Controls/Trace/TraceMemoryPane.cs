using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

    private const int LargeMarkSize = 2;

    private const int MarkDensity = 4;

    private const int DebounceMilliseconds = 120;

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

    private Structure? CachedStructure { get; set; }

    private Frame? CurrentFrame { get; set; }

    private FrameKey? RequestedKey { get; set; }

    private CancellationTokenSource? Rendering { get; set; }

    private Dictionary<int, SKColor> Colours { get; } = [];

    private Dictionary<SKColor, List<SKPoint>> PointsByColour { get; } = [];

    private SKPaint PointPaint { get; } = new() { StrokeWidth = 1, StrokeCap = SKStrokeCap.Square };

    public void SetBlocks(IReadOnlyList<TraceMemoryBlock>? blocks)
    {
        Blocks = blocks is null ? [] : [.. blocks.OrderBy(b => b.Address)];

        Reset();

        Hover = null;
    }

    public void SetScale(TraceScale scale)
    {
        if (ReferenceEquals(TraceScale, scale))
        {
            return;
        }

        TraceScale = scale;

        Reset();
    }

    public bool Contains(Point position) => HasBlocks && position.Y >= Top && position.Y < Top + Height;

    public void Draw(SKCanvas canvas, int width, double scale)
    {
        Paints.Fill.Color = PaneColour;

        canvas.DrawRect(0, Top, width, Height, Paints.Fill);

        var key = new FrameKey(Blocks, TraceScale, Host.Axis, width, Top, Height, Host.ViewStart, Host.ViewEnd, scale);

        if (CurrentFrame is { } frame && frame.Key == key)
        {
            canvas.DrawPicture(frame.Picture);

            return;
        }

        RequestFrame(key);
    }

    public void Render(int width, double scale)
    {
        var key = new FrameKey(Blocks, TraceScale, Host.Axis, width, Top, Height, Host.ViewStart, Host.ViewEnd, scale);

        CancelRendering();

        RequestedKey = key;

        SetFrame(key, Build(key, CancellationToken.None));

        Host.ShowMemoryProgress(false);
    }

    public void DrawOverlay(SKCanvas canvas, int width, double scale)
    {
        if (Hover is not { } hit || hit.Block >= Blocks.Count || GetCurrentFrame() is not { } frame)
        {
            return;
        }

        var block = Blocks[hit.Block];

        canvas.Save();

        canvas.ClipRect(new SKRect(0, Top, width, Top + Height));

        if (frame.View.BlockRows[hit.Block] is { } rows)
        {
            var left = Math.Max(GetX(block.From, frame.Key), -1f);

            var right = block.To is { } to ? Math.Min(GetX(to, frame.Key), width + 1f) : width + 1f;

            Paints.Outline.Color = HoverColour;

            Paints.Outline.StrokeWidth = 1f;

            canvas.DrawRect(new SKRect(left, Top + rows.Top, right, Top + rows.Bottom), Paints.Outline);
        }

        canvas.Restore();
    }

    public TraceMemoryHit? GetHit(Point position)
    {
        if (!Contains(position) || GetCurrentFrame() is not { } frame)
        {
            return null;
        }

        var scale = Host.Scale;

        if (frame.View.GetAddressAt(frame.Structure, (float)(position.Y - Top)) is not { } address)
        {
            return null;
        }

        var value = frame.Key.Start + position.X / scale;

        var tolerance = HitWidth / scale;

        for (var index = 0; index < Blocks.Count; index++)
        {
            var block = Blocks[index];

            if (frame.View.BlockRows[index] is null || address < block.Address || address >= block.Address + (ulong)block.Size)
            {
                continue;
            }

            var from = TraceScale.GetValue(block.From, frame.Key.Axis);

            var to = block.To is { } end ? TraceScale.GetValue(end, frame.Key.Axis) : double.MaxValue;

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

        return string.Join(Environment.NewLine, block.Label, $"0x{block.Address:X16} +0x{found.Offset:X}, {block.Size} Bytes");
    }

    public void Click(Point position)
    {
        if (GetClickHit(position) is not { } hit)
        {
            return;
        }

        var block = Blocks[hit.Block];

        if (hit.Write >= 0)
        {
            Host.MovePlayhead(TraceScale.GetValue(block.Writes[hit.Write].Point, Host.Axis));
        }

        Host.ShowMemory(block.Block,
                        block.Address + (ulong)hit.Offset,
                        hit.Write >= 0 ? block.Writes[hit.Write].Point.Position : block.From.Position);
    }

    public void DoubleClick(Point position)
    {
        if (GetClickHit(position) is not { } hit)
        {
            return;
        }

        var block = Blocks[hit.Block];

        Host.SelectMemoryWrite(block.Block, hit.Write >= 0 ? block.Writes[hit.Write].Point.Position : null);
    }

    public void Dispose()
    {
        Reset();

        PointPaint.Dispose();
    }

    private void Reset()
    {
        CancelRendering();

        RequestedKey = null;

        CachedStructure = null;

        CurrentFrame?.Picture.Dispose();

        CurrentFrame = null;

        Host.ShowMemoryProgress(false);
    }

    private void CancelRendering()
    {
        Rendering?.Cancel();

        Rendering?.Dispose();

        Rendering = null;
    }

    private void RequestFrame(FrameKey key)
    {
        if (RequestedKey == key)
        {
            return;
        }

        RequestedKey = key;

        CancelRendering();

        Rendering = new CancellationTokenSource();

        Host.ShowMemoryProgress(true);

        _ = RenderAsync(key, Rendering.Token);
    }

    private async Task RenderAsync(FrameKey key, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceMilliseconds, cancellationToken).ConfigureAwait(false);

            var rendered = Build(key, cancellationToken);

            Host.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested || RequestedKey != key)
                {
                    return;
                }

                SetFrame(key, rendered);

                Host.ShowMemoryProgress(false);

                Host.Invalidate();
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Frame? GetCurrentFrame()
        => CurrentFrame is { } frame
           && ReferenceEquals(frame.Key.Blocks, Blocks)
           && ReferenceEquals(frame.Key.TraceScale, TraceScale)
           && frame.Key.Axis == Host.Axis
           && frame.Key.Top == Top
           && frame.Key.Height == Height
           && frame.Key.Start == Host.ViewStart
           && frame.Key.End == Host.ViewEnd
            ? frame
            : null;

    private TraceMemoryHit? GetClickHit(Point position)
    {
        if (!Contains(position) || GetCurrentFrame() is not { } frame)
        {
            return null;
        }

        return GetNearestWrite(frame, position.X, (float)(position.Y - Top), Host.Scale) ?? GetHit(position);
    }

    private void SetFrame(FrameKey key, Rendered rendered)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, key.Top, key.Width, key.Top + key.Height));

        canvas.ClipRect(new SKRect(0, key.Top, key.Width, key.Top + key.Height));

        for (var block = 0; block < key.Blocks.Count; block++)
        {
            DrawLifetime(canvas, key, rendered.View, block);
        }

        DrawPoints(canvas, rendered.Marks, rendered.View.MarkSize);

        CurrentFrame?.Picture.Dispose();

        CurrentFrame = new Frame(key, rendered.Structure, rendered.View, recorder.EndRecording());
    }

    private void DrawPoints(SKCanvas canvas, List<(int X, int Y, int CallNodeIndex)> marks, int size)
    {
        PointPaint.StrokeWidth = size;

        Colours.Clear();

        foreach (var points in PointsByColour.Values)
        {
            points.Clear();
        }

        foreach (var (x, y, callNodeIndex) in marks)
        {
            var colour = GetColour(callNodeIndex);

            if (!PointsByColour.TryGetValue(colour, out var points))
            {
                points = [];

                PointsByColour[colour] = points;
            }

            points.Add(new SKPoint(x + size / 2f, y + size / 2f));
        }

        foreach (var (colour, points) in PointsByColour)
        {
            if (points.Count == 0)
            {
                continue;
            }

            PointPaint.Color = colour;

            canvas.DrawPoints(SKPointMode.Points, [.. points], PointPaint);
        }
    }

    private void DrawLifetime(SKCanvas canvas, FrameKey key, View view, int index)
    {
        if (view.BlockRows[index] is not { } rows)
        {
            return;
        }

        var block = key.Blocks[index];

        var left = GetX(block.From, key);

        var right = block.To is { } to ? GetX(to, key) : key.Width;

        if (right < 0 || left > key.Width)
        {
            return;
        }

        var barLeft = Math.Max(left, -1f);

        Paints.Fill.Color = LifetimeColour;

        canvas.DrawRect(barLeft,
                        key.Top + rows.Top,
                        Math.Max(1f, Math.Min(right, key.Width + 1f) - barLeft),
                        rows.Bottom - rows.Top,
                        Paints.Fill);
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

    private TraceMemoryHit? GetNearestWrite(Frame frame, double x, float y, double scale)
    {
        TraceMemoryHit? found = null;

        var nearest = double.MaxValue;

        var value = frame.Key.Start + x / scale;

        var (structure, view) = (frame.Structure, frame.View);

        for (var block = 0; block < Blocks.Count; block++)
        {
            if (view.BlockRows[block] is not { } blockRows || y < blockRows.Top - HitHeight || y > blockRows.Bottom + HitHeight)
            {
                continue;
            }

            var writes = Blocks[block].Writes;

            foreach (var laneIndex in structure.BlockLanes[block])
            {
                var lane = structure.Lanes[laneIndex];

                if (view.GetRows(structure, lane.Run, lane.Address, lane.Length) is not { } rows)
                {
                    continue;
                }

                var dy = Math.Abs((rows.Top + rows.Bottom) / 2 - y);

                if (dy > HitHeight + (rows.Bottom - rows.Top) / 2)
                {
                    continue;
                }

                var after = GetLowerBound(lane.Values, value, 0);

                for (var index = Math.Max(after - 1, 0); index <= after && index < lane.Writes.Length; index++)
                {
                    var write = lane.Writes[index];

                    var dx = Math.Abs((lane.Values[index] - value) * scale);

                    if (dx > HitWidth)
                    {
                        continue;
                    }

                    var distance = dx * dx + dy * dy;

                    if (distance < nearest)
                    {
                        nearest = distance;

                        found = new TraceMemoryHit(block, write, writes[write].Offset);
                    }
                }
            }
        }

        return found;
    }

    private Rendered Build(FrameKey key, CancellationToken cancellationToken)
    {
        var structure = GetStructure(key, cancellationToken);

        var view = GetView(structure, key);

        var columns = Math.Max(key.Width, 1) / view.MarkSize + 1;

        var stamps = new bool[columns * ((int)MathF.Ceiling(key.Height) + view.MarkSize + 1)];

        var marks = new List<(int X, int Y, int CallNodeIndex)>();

        foreach (var lane in structure.Lanes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddMarks(lane, structure, view, key, columns, stamps, marks);
        }

        return new Rendered(structure, view, marks);
    }

    private static void AddMarks(Lane lane,
                                 Structure structure,
                                 View view,
                                 FrameKey key,
                                 int columns,
                                 bool[] stamps,
                                 List<(int X, int Y, int CallNodeIndex)> marks)
    {
        if (view.GetRows(structure, lane.Run, lane.Address, lane.Length) is not { } rows)
        {
            return;
        }

        var writes = structure.Blocks[lane.Block].Writes;

        var (start, scale, size) = (key.Start, key.Scale, view.MarkSize);

        var end = key.End + 1 / scale;

        var y = (int)MathF.Floor(key.Top + (rows.Top + rows.Bottom - size) / 2);

        var row = (y - (int)MathF.Floor(key.Top)) * columns;

        var values = lane.Values;

        var index = GetLowerBound(values, start - size / scale, 0);

        while (index < values.Length)
        {
            var value = values[index];

            if (value > end)
            {
                break;
            }

            var column = (int)Math.Floor((value - start) * scale / size);

            if (column * size >= key.Width)
            {
                break;
            }

            if (column >= 0 && IsFirstAt(stamps, row + column))
            {
                marks.Add((column * size, y, writes[lane.Writes[index]].CallNodeIndex));
            }

            index = GetLowerBound(values, start + (column + 1) * size / scale, index + 1);
        }
    }

    private static bool IsFirstAt(bool[] stamps, int cell)
    {
        if (cell < 0 || cell >= stamps.Length)
        {
            return true;
        }

        if (stamps[cell])
        {
            return false;
        }

        stamps[cell] = true;

        return true;
    }

    private static int GetLowerBound(double[] values, double value, int from)
    {
        var low = from;

        var bound = from;

        var step = 1;

        while (bound < values.Length && values[bound] < value)
        {
            low = bound + 1;

            bound = low + step;

            step <<= 1;
        }

        var high = Math.Min(bound, values.Length);

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

    private static int GetCount(Lane lane, double from, double to)
    {
        var first = GetLowerBound(lane.Values, from, 0);

        return GetLowerBound(lane.Values, Math.BitIncrement(to), first) - first;
    }

    private static bool HasWriteBetween(Lane lane, double from, double to)
    {
        var index = GetLowerBound(lane.Values, from, 0);

        return index < lane.Values.Length && lane.Values[index] <= to;
    }

    private static float GetX(TracePoint point, FrameKey key)
        => (float)((key.TraceScale.GetValue(point, key.Axis) - key.Start) * key.Scale);

    private Structure GetStructure(FrameKey key, CancellationToken cancellationToken)
    {
        if (CachedStructure is { } cached
            && ReferenceEquals(cached.Blocks, key.Blocks)
            && ReferenceEquals(cached.TraceScale, key.TraceScale)
            && cached.Axis == key.Axis)
        {
            return cached;
        }

        var blocks = key.Blocks;

        var drafts = new List<(ulong Address, int Length, int[] Writes, double[] Values)>[blocks.Count];

        for (var block = 0; block < blocks.Count; block++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            drafts[block] = GetLaneDrafts(blocks[block], key.TraceScale, key.Axis);
        }

        var runs = new List<Run>();

        var blockRuns = new (int First, int Last)[blocks.Count];

        var lanes = new List<Lane>();

        var blockLanes = new int[blocks.Count][];

        var segment = -1;

        var index = 0;

        while (index < blocks.Count)
        {
            var first = index;

            var end = blocks[index].Address + (ulong)blocks[index].Size;

            while (index + 1 < blocks.Count && blocks[index + 1].Address < end)
            {
                index++;

                end = Math.Max(end, blocks[index].Address + (ulong)blocks[index].Size);
            }

            segment++;

            var firstRun = runs.Count;

            var extents = new List<(ulong Start, ulong End)>();

            for (var block = first; block <= index; block++)
            {
                extents.AddRange(drafts[block].Select(d => (d.Address, d.Address + (ulong)d.Length)));
            }

            runs.AddRange(Merge(extents).Select(e => new Run(e.Start, e.End, segment)));

            for (var block = first; block <= index; block++)
            {
                var start = blocks[block].Address;

                var stop = start + (ulong)blocks[block].Size;

                blockRuns[block] = (GetRunAt(runs, firstRun, runs.Count, start), GetRunAt(runs, firstRun, runs.Count, stop) - 1);

                blockLanes[block] = [.. Enumerable.Range(lanes.Count, drafts[block].Count)];

                lanes.AddRange(drafts[block].Select(d => new Lane(block,
                                                                  d.Address,
                                                                  d.Length,
                                                                  GetRunAt(runs, firstRun, runs.Count, d.Address),
                                                                  d.Writes,
                                                                  d.Values)));
            }

            index++;
        }

        var runLanes = runs.Select(_ => new List<int>()).ToArray();

        for (var lane = 0; lane < lanes.Count; lane++)
        {
            runLanes[lanes[lane].Run].Add(lane);
        }

        var structure = new Structure(blocks,
                                      key.TraceScale,
                                      key.Axis,
                                      [.. runs],
                                      blockRuns,
                                      [.. lanes],
                                      blockLanes,
                                      [.. runLanes.Select(l => l.ToArray())]);

        CachedStructure = structure;

        return structure;
    }

    private static List<(ulong Address, int Length, int[] Writes, double[] Values)> GetLaneDrafts(TraceMemoryBlock block,
                                                                                                    TraceScale traceScale,
                                                                                                    TraceTimelineAxis axis)
    {
        var writes = block.Writes;

        var byExtent = new Dictionary<(int Offset, int Length), List<int>>();

        (int Offset, int Length) lastExtent = (-1, -1);

        List<int>? last = null;

        for (var index = 0; index < writes.Length; index++)
        {
            var extent = (writes[index].Offset, writes[index].Length);

            if (extent.Length <= 0)
            {
                continue;
            }

            if (last is null || extent != lastExtent)
            {
                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(byExtent, extent, out _);

                list ??= [];

                last = list;

                lastExtent = extent;
            }

            last.Add(index);
        }

        var drafts = new List<(ulong Address, int Length, int[] Writes, double[] Values)>(byExtent.Count);

        foreach (var ((offset, length), list) in byExtent.OrderBy(e => e.Key))
        {
            var lane = list.ToArray();

            var values = new double[lane.Length];

            for (var index = 0; index < lane.Length; index++)
            {
                values[index] = traceScale.GetValue(writes[lane[index]].Point, axis);
            }

            SortByValue(values, lane);

            drafts.Add((block.Address + (ulong)offset, length, lane, values));
        }

        return drafts;
    }

    private static void SortByValue(double[] values, int[] lane)
    {
        for (var index = 1; index < values.Length; index++)
        {
            if (values[index] < values[index - 1])
            {
                Array.Sort(values, lane);

                return;
            }
        }
    }

    private static View GetView(Structure structure, FrameKey key)
    {
        var (height, start, end, scale) = (key.Height, key.Start, key.End, key.Scale);

        var margin = 1 / scale;

        var runs = structure.Runs;

        var active = new List<int>();

        for (var run = 0; run < runs.Length; run++)
        {
            foreach (var lane in structure.RunLanes[run])
            {
                if (HasWriteBetween(structure.Lanes[lane], start - margin, end + margin))
                {
                    active.Add(run);

                    break;
                }
            }
        }

        var densest = 0;

        foreach (var run in active)
        {
            foreach (var lane in structure.RunLanes[run])
            {
                densest = Math.Max(densest, GetCount(structure.Lanes[lane], start, end));
            }
        }

        var markSize = densest / MarkDensity < (end - start) * scale ? LargeMarkSize : 1;

        var segments = new List<List<int>>();

        foreach (var run in active)
        {
            if (segments.Count == 0 || runs[segments[^1][0]].Segment != runs[run].Segment)
            {
                segments.Add([]);
            }

            segments[^1].Add(run);
        }

        var gap = segments.Count > 1
            ? Math.Clamp((height - segments.Count * MinimumRunHeight) / (segments.Count - 1), 0, BlockGap)
            : 0;

        var segmentHeights = GetRunHeights([.. segments.Select(s => s.Aggregate(0ul, (total, r) => total + runs[r].End - runs[r].Start))],
                                           height - gap * Math.Max(segments.Count - 1, 0));

        var tops = new float[runs.Length];

        var bytePixels = new float[runs.Length];

        Array.Fill(tops, float.NaN);

        var y = 0f;

        for (var segment = 0; segment < segments.Count; segment++)
        {
            if (segment > 0)
            {
                y += gap;
            }

            var members = segments[segment];

            var heights = GetRunHeights([.. members.Select(r => runs[r].End - runs[r].Start)], segmentHeights[segment]);

            for (var index = 0; index < members.Count; index++)
            {
                var run = members[index];

                tops[run] = y;

                bytePixels[run] = heights[index] / (runs[run].End - runs[run].Start);

                y += heights[index];
            }
        }

        var blockRows = new (float Top, float Bottom)?[structure.BlockRuns.Length];

        for (var block = 0; block < blockRows.Length; block++)
        {
            var (firstRun, lastRun) = structure.BlockRuns[block];

            float? top = null;

            var bottom = 0f;

            for (var run = firstRun; run <= lastRun; run++)
            {
                if (float.IsNaN(tops[run]))
                {
                    continue;
                }

                top ??= tops[run];

                bottom = tops[run] + (runs[run].End - runs[run].Start) * bytePixels[run];
            }

            blockRows[block] = top is { } value ? (value, bottom) : null;
        }

        return new View(markSize, [.. active], tops, bytePixels, blockRows);
    }

    private static int GetRunAt(IReadOnlyList<Run> runs, int from, int to, ulong address)
    {
        var low = from;

        var high = to;

        while (low < high)
        {
            var middle = (low + high) / 2;

            if (runs[middle].End <= address)
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

    private static List<(ulong Start, ulong End)> Merge(List<(ulong Start, ulong End)> extents)
    {
        var merged = new List<(ulong Start, ulong End)>();

        foreach (var extent in extents.OrderBy(e => e.Start))
        {
            if (merged.Count > 0 && merged[^1].End >= extent.Start)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, extent.End));

                continue;
            }

            merged.Add(extent);
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

    private sealed record Run(ulong Start, ulong End, int Segment);

    private sealed record Lane(int Block, ulong Address, int Length, int Run, int[] Writes, double[] Values);

    private sealed record Structure(IReadOnlyList<TraceMemoryBlock> Blocks,
                                    TraceScale TraceScale,
                                    TraceTimelineAxis Axis,
                                    Run[] Runs,
                                    (int First, int Last)[] BlockRuns,
                                    Lane[] Lanes,
                                    int[][] BlockLanes,
                                    int[][] RunLanes);

    private readonly record struct FrameKey(IReadOnlyList<TraceMemoryBlock> Blocks,
                                            TraceScale TraceScale,
                                            TraceTimelineAxis Axis,
                                            int Width,
                                            float Top,
                                            float Height,
                                            double Start,
                                            double End,
                                            double Scale);

    private sealed record Frame(FrameKey Key, Structure Structure, View View, SKPicture Picture);

    private sealed record Rendered(Structure Structure, View View, List<(int X, int Y, int CallNodeIndex)> Marks);

    private sealed record View(int MarkSize,
                               int[] Active,
                               float[] Tops,
                               float[] BytePixels,
                               (float Top, float Bottom)?[] BlockRows)
    {
        public (float Top, float Bottom)? GetRows(Structure structure, ulong address, int length)
            => GetRows(structure, GetRunAt(structure.Runs, 0, structure.Runs.Length, address), address, length);

        public (float Top, float Bottom)? GetRows(Structure structure, int run, ulong address, int length)
        {
            if (run >= Tops.Length || float.IsNaN(Tops[run]) || address < structure.Runs[run].Start)
            {
                return null;
            }

            var top = Tops[run] + (address - structure.Runs[run].Start) * BytePixels[run];

            return (top, top + Math.Max(MinimumRunHeight, length * BytePixels[run]));
        }

        public ulong? GetAddressAt(Structure structure, float y)
        {
            var low = 0;

            var high = Active.Length;

            while (low < high)
            {
                var middle = (low + high) / 2;

                if (Tops[Active[middle]] <= y)
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

            var run = structure.Runs[Active[low - 1]];

            var bytePixels = BytePixels[Active[low - 1]];

            var top = Tops[Active[low - 1]];

            return y < top + (run.End - run.Start) * bytePixels
                ? run.Start + Math.Min((ulong)((y - top) / bytePixels), run.End - run.Start - 1)
                : null;
        }
    }
}
