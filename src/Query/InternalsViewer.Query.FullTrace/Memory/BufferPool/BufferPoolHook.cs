using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;

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
/// Two function types are hooked:
///
/// - Get       - functions returning a BUF. The frame, page, file and database are read from the BUF fields at the return point.
///
/// - PostRead  - page read completion. The frame and page number arguments are captured at entry and validated against the frame's page
///               header at return. These calls carry a File Control Block rather than a database id - the database is resolved later by
///               correlation, when a Get observes the same frame holding the same page the BUF's database id is recorded against the FCB,
///               and Build applies it to the reads.
///
/// Frames are ignored unless they are page size aligned and, for reads, the frame header matches the expected page number.
///
/// The hook produces the TraceBufferPool of frame/page/database entries (Build) and the BUF descriptors seen, as memory blocks
/// (GetBuffers).
/// </remarks>
internal sealed class BufferPoolHook(AddressTable<BufferPoolFunctionType> functions) : ITraceHook
{
    private const ulong HeaderPageIdOffset = 0x20;

    private const ulong HeaderFileIdOffset = 0x24;

    private const int FileControlBlockArgument = 0;

    private const int FrameArgument = 1;

    private const int PageNumberArgument = 2;

    private const int LatchArgument = 0;

    public IEnumerable<TraceHookFunction> Functions
        => functions.Select(f => new TraceHookFunction(f.Key,
                                                       f.Value == BufferPoolFunctionType.LatchRelease
                                                           ? TraceHookEvents.Entry
                                                           : TraceHookEvents.Entry | TraceHookEvents.Return));

    private TraceBufferPool.Builder Pages { get; } = new();

    private Dictionary<(uint Thread, ulong Id), CallStart> Starts { get; } = [];

    private List<PageRead> Reads { get; } = [];

    private Dictionary<ulong, PageRead> LatestReads { get; } = [];

    private Dictionary<ulong, ushort> FileDatabases { get; } = [];

    private Dictionary<ulong, (uint Thread, TracePosition Position)> Buffers { get; } = [];

    private HashSet<(ulong Frame, PageAddress Page)> Layouts { get; } = [];

    private Dictionary<ulong, FrameLayout> PendingLayouts { get; } = [];

    private static int MaximumSlotCount => (PageData.Size - PageHeader.Size) / sizeof(ushort);

    public TraceBufferPool Build()
    {
        foreach (var read in Reads)
        {
            if (FileDatabases.TryGetValue(read.FileControlBlock, out var databaseId))
            {
                Pages.Add(new TraceBufferPage(read.Frame, read.Page, databaseId, read.Position));

                if (read.Layout is { Offsets: { } offsets } layout && Layouts.Add((read.Frame, read.Page)))
                {
                    Pages.AddLayout(read.Page, databaseId, offsets, layout.Type);
                }
            }
        }

        foreach (var buffer in PendingLayouts.Keys.ToList())
        {
            FinishLayout(buffer);
        }

        return Pages.Build();
    }

    public MemoryBlock[] GetBuffers()
        => [.. Buffers.Select(b => new MemoryBlock(b.Key, BufferDescriptor.Size, b.Value.Thread, b.Value.Position, TracePosition.Unknown))];

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        var type = functions.TryGetValue(call.FunctionAddress, out var value) ? value : BufferPoolFunctionType.Get;

        if (type == BufferPoolFunctionType.LatchRelease)
        {
            OnLatchRelease(call, replay);

            return;
        }

        Starts[(call.Thread, call.Id)] = type == BufferPoolFunctionType.PostRead && call.Arguments.Length > PageNumberArgument
            ? new CallStart(call.Position,
                            call.Arguments[FileControlBlockArgument],
                            call.Arguments[FrameArgument],
                            (uint)call.Arguments[PageNumberArgument])
            : new CallStart(call.Position, 0, 0, 0);
    }

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

        var page = new PageAddress(fileId, (int)pageId);

        var layout = new FrameLayout(start.Frame, page, 0);

        layout.Update(replay);

        var read = new PageRead(start.FileControlBlock, start.Frame, page, start.Position, layout);

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

        TrackLayout(buffer, frame, page, databaseId, replay);

        if (!Buffers.TryGetValue(buffer, out var first) || start < first.Position)
        {
            Buffers[buffer] = (call.Thread, start);
        }
    }

    private void TrackLayout(ulong buffer, ulong frame, PageAddress page, ushort databaseId, ITraceReplay replay)
    {
        if (Layouts.Contains((frame, page)))
        {
            return;
        }

        if (PendingLayouts.TryGetValue(buffer, out var pending) && (pending.Frame != frame || pending.Page != page))
        {
            FinishLayout(buffer);
        }

        if (!PendingLayouts.TryGetValue(buffer, out var layout))
        {
            layout = new FrameLayout(frame, page, databaseId);

            PendingLayouts[buffer] = layout;
        }

        UpdateLayout(buffer, layout, replay);
    }

    private void OnLatchRelease(in TraceHookCall call, ITraceReplay replay)
    {
        if (call.Arguments.Length > LatchArgument
            && call.Arguments[LatchArgument] - BufferDescriptor.LatchWaitersOffset is var buffer
            && PendingLayouts.TryGetValue(buffer, out var layout))
        {
            UpdateLayout(buffer, layout, replay);
        }
    }

    private void UpdateLayout(ulong buffer, FrameLayout layout, ITraceReplay replay)
    {
        layout.Update(replay);

        if (layout.IsComplete)
        {
            FinishLayout(buffer);
        }
    }

    private void FinishLayout(ulong buffer)
    {
        if (!PendingLayouts.Remove(buffer, out var layout) || layout.Offsets is not { } offsets)
        {
            return;
        }

        if (Layouts.Add((layout.Frame, layout.Page)))
        {
            Pages.AddLayout(layout.Page, layout.DatabaseId, offsets, layout.Type);
        }
    }

    private bool IsPostRead(in TraceHookCall call)
        => functions.TryGetValue(call.FunctionAddress, out var type) && type == BufferPoolFunctionType.PostRead;

    private readonly record struct CallStart(TracePosition Position, ulong FileControlBlock, ulong Frame, uint PageNumber);

    private readonly record struct PageRead(ulong FileControlBlock,
                                            ulong Frame,
                                            PageAddress Page,
                                            TracePosition Position,
                                            FrameLayout Layout);

    private sealed class FrameLayout(ulong frame, PageAddress page, ushort databaseId)
    {
        public ulong Frame => frame;

        public PageAddress Page => page;

        public ushort DatabaseId => databaseId;

        public PageType Type { get; private set; } = PageType.None;

        public ushort[]? Offsets { get; private set; }

        public bool IsComplete => Type != PageType.None && Offsets is not null && Unknown == 0;

        private int Unknown { get; set; }

        public void Update(ITraceReplay replay)
        {
            if (Type == PageType.None && replay.TryRead(frame + PageHeaderParser.PageTypeOffset, out byte type))
            {
                Type = (PageType)type;
            }

            if (Offsets is null)
            {
                if (!replay.TryRead(frame + PageHeaderParser.SlotCountOffset, out ushort count) || count > MaximumSlotCount)
                {
                    return;
                }

                Offsets = new ushort[count];

                Unknown = count;
            }

            for (var slot = 0; slot < Offsets.Length && Unknown > 0; slot++)
            {
                if (Offsets[slot] == 0
                    && replay.TryRead(frame + PageData.Size - (ulong)((slot + 1) * sizeof(ushort)), out ushort offset)
                    && offset != 0)
                {
                    Offsets[slot] = offset;

                    Unknown--;
                }
            }
        }
    }
}
