using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

/// <summary>
/// Hook to capture BUF buffer pool structure
/// </summary>
/// <remarks>
/// The buffer pool contains 8 KB frames that are slots for pages to be loaded into. It manages the frames with an array of BUF structures,
/// one for each frame.
///
/// BUFs are the link between a database + page address (e.g. 1:200) and the memory address for the frame that is currently holding that
/// page.
///
/// Query execution uses the BUF to get the address of the frame holding a page at that point in time, and may also hold addresses inside
/// the frame directly, such as the address of a record. The latch in the BUF keeps the page in its frame and protects its bytes while the
/// page is being read or changed.
///
/// Page contents are not captured in the trace, but every page access goes through the page’s BUF. Capturing the BUF gives the frame
/// address, page address and database, because all three are fields of the BUF.
///
/// 
/// </remarks>
internal sealed class BufferPoolHook(BufferPoolFunctions functions) : ITraceHook
{
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

    private Dictionary<ulong, (uint Thread, TracePosition Position)> Buffers { get; } = [];

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

    public MemoryBlock[] GetBuffers()
        => [.. Buffers.Select(b => new MemoryBlock(b.Key, BufferDescriptor.Size, b.Value.Thread, b.Value.Position, TracePosition.Unknown))];

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
            || !replay.TryRead(buffer + BufferDescriptor.FrameOffset, out ulong frame)
            || frame == 0
            || frame % PageData.Size != 0
            || !replay.TryRead(buffer + BufferDescriptor.PageIdOffset, out int pageId)
            || !replay.TryRead(buffer + BufferDescriptor.FileIdOffset, out short fileId)
            || !replay.TryRead(buffer + BufferDescriptor.DatabaseIdOffset, out ushort databaseId))
        {
            return;
        }

        var page = new PageAddress(fileId, pageId);

        if (LatestReads.TryGetValue(frame, out var read) && read.Page == page)
        {
            FileDatabases[read.FileControlBlock] = databaseId;
        }

        Pages.Add(new TraceBufferPage(frame, page, databaseId, start, buffer));

        if (!Buffers.TryGetValue(buffer, out var first) || start < first.Position)
        {
            Buffers[buffer] = (call.Thread, start);
        }
    }

    private bool IsPostRead(in TraceHookCall call) => PostReads.Contains(call.FunctionAddress);

    private readonly record struct CallStart(TracePosition Position, ulong FileControlBlock, ulong Frame, uint PageNumber);

    private readonly record struct PageRead(ulong FileControlBlock, ulong Frame, PageAddress Page, TracePosition Position);
}
