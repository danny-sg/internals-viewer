using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

internal sealed class BufferPoolHook(BufferPoolFunctions functions) : ITraceHook
{
    private const ulong FrameOffset = 0x00;

    private const ulong DatabaseIdOffset = 0x2C;

    private const ulong PageIdOffset = 0x70;

    private const ulong FileIdOffset = 0x74;

    private const ulong HeaderPageIdOffset = 0x20;

    private const ulong HeaderFileIdOffset = 0x24;

    private const int FileControlBlockArgument = 0;

    private const int FrameArgument = 1;

    private const int PageNumberArgument = 2;

    public IEnumerable<TraceHookFunction> Functions
        => functions.Gets.Concat(functions.PostReads).Select(f => new TraceHookFunction(f, TraceHookEvents.Entry | TraceHookEvents.Return));

    private HashSet<ulong> PostReads { get; } = [.. functions.PostReads];

    private TraceBufferPool.Builder Pages { get; } = new();

    private Dictionary<(uint Thread, ulong Id), CallStart> Starts { get; } = [];

    private List<PageRead> Reads { get; } = [];

    private Dictionary<ulong, PageRead> LatestReads { get; } = [];

    private Dictionary<ulong, ushort> FileDatabases { get; } = [];

    public TraceBufferPool Build()
    {
        foreach (var read in Reads)
        {
            if (FileDatabases.TryGetValue(read.FileControlBlock, out var databaseId))
            {
                Pages.Add(new TraceBufferPage(read.Frame, read.Page, databaseId, read.Position));
            }
        }

        return Pages.Build();
    }

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
        => Starts[(call.Thread, call.Id)] = IsPostRead(call) && call.Arguments.Length > PageNumberArgument
            ? new CallStart(call.Position,
                            call.Arguments[FileControlBlockArgument],
                            call.Arguments[FrameArgument],
                            (uint)call.Arguments[PageNumberArgument])
            : new CallStart(call.Position, 0, 0, 0);

    public void OnAbandoned(in TraceHookCall call) => Starts.Remove((call.Thread, call.Id));

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        var start = Starts.Remove((call.Thread, call.Id), out var entry) ? entry : new CallStart(call.Position, 0, 0, 0);

        if (IsPostRead(call))
        {
            AddRead(start, replay);
        }
        else
        {
            AddGet(call, start.Position, replay);
        }
    }

    private void AddRead(CallStart start, ITraceReplay replay)
    {
        if (start.Frame == 0
            || start.Frame % PageData.Size != 0
            || !replay.TryRead(start.Frame + HeaderPageIdOffset, out uint pageId)
            || pageId != start.PageNumber
            || !replay.TryRead(start.Frame + HeaderFileIdOffset, out short fileId))
        {
            return;
        }

        var read = new PageRead(start.FileControlBlock, start.Frame, new PageAddress(fileId, (int)pageId), start.Position);

        Reads.Add(read);

        LatestReads[read.Frame] = read;
    }

    private void AddGet(in TraceHookCall call, TracePosition start, ITraceReplay replay)
    {
        var buffer = call.ReturnValue;

        if (buffer == 0
            || !replay.TryRead(buffer + FrameOffset, out ulong frame)
            || frame == 0
            || frame % PageData.Size != 0
            || !replay.TryRead(buffer + PageIdOffset, out int pageId)
            || !replay.TryRead(buffer + FileIdOffset, out short fileId)
            || !replay.TryRead(buffer + DatabaseIdOffset, out ushort databaseId))
        {
            return;
        }

        var page = new PageAddress(fileId, pageId);

        if (LatestReads.TryGetValue(frame, out var read) && read.Page == page)
        {
            FileDatabases[read.FileControlBlock] = databaseId;
        }

        Pages.Add(new TraceBufferPage(frame, page, databaseId, start, buffer));
    }

    private bool IsPostRead(in TraceHookCall call) => PostReads.Contains(call.FunctionAddress);

    private readonly record struct CallStart(TracePosition Position, ulong FileControlBlock, ulong Frame, uint PageNumber);

    private readonly record struct PageRead(ulong FileControlBlock, ulong Frame, PageAddress Page, TracePosition Position);
}
