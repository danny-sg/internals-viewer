using System.Runtime.InteropServices;
using System.Text;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.Query.FullTrace.Native;

/// <summary>
/// P/Invoke surface for the TimeTravelBridge native library
/// </summary>
/// <remarks>
/// Rather complicated, but we need to use a native bridge DLL to talk to the TTD replay library as it can't be bound to directly.
///
/// The native bridge is built from the C++ source in the InternalsViewer.Query.TimeTravelBridge project, which is a C++ project that
/// wraps the TTD replay library and exposes a C-style API for use in this managed code.
///
/// High level, the API takes a trace and replays it forward allowing stack frames, arguments, and return values to be captured and
/// buffered locally before being flushed. The flat forward read uses a local stack to replicate the entry/exit (push/pop) of nested
/// functions as return values are only available at the exit point.
///
/// The replay uses several function indicators to dictate behaviour, excluded functions to ignore, instance methods, value locators where
/// arguments and return results are pointers which should be followed in the trace memory at the current position for actual values, and
/// functions that publish extended events.
///
/// The replay also reports progress through a callback, and stops when the caller sets the cancellation flag.
///
/// All addresses passed to and from the bridge are absolute addresses in the recorded process.
///
/// The bridge does the following:
///
/// 1. Opens the trace
///
///    OpenTrace loads the TTD replay library, creates a replay engine, initialises it with the trace file and reads the module list.
///    It returns a TraceHandle that calls CloseTrace when it is released.
///
/// 2. Gets the list of modules in the trace via GetModuleCount() and GetModule()
///
/// 3. Replays the trace
///
///    ReadCallTree makes a single forward pass through the trace and builds a call tree of the calls made on the specified thread ids
///    (every thread if none are given). There is one node per distinct (parent, function, instance), with a count of its calls.
///
///    The replay returns a handle that is used with GetCallNodeCount() and GetCallNodes() to retrieve the tree, then freed with
///    CloseCallTree().
///
///    As the cursor moves through the trace the bridge uses callbacks to provide detail that is linked to a node index in the call tree.
///    Nodes are only ever appended, so an index received during the replay matches the final tree.
///
///    ReadCallTree takes the following inputs:
///
///    - Thread Ids
///
///      Only calls on these threads are recorded. An empty list records every thread.
///
///    - Excluded Functions
///
///      If an address is in the excluded list, the call and everything it calls are not added to the tree, the call log or the timeline.
///      The replay still runs through it, but nested calls are ignored until it returns, and its time is counted in the parent's span.
///
///    - Instance Methods
///
///      If an address is in the instance methods list, the value of its instance register is used as the call's instance. The instance
///      is part of the tree node and call log key, so calls to the same function on different objects are kept apart. The register is
///      RCX for member functions, or the keyed pointer argument for static functions. Calls to any other function have an instance of
///      zero.
///
///      Each entry is packed by PackInstanceMethod as the address with the register index (0-3 for RCX, RDX, R8, R9) in the top 4 bits.
///
///    - Pointer Reads
///
///      Pointer Reads define functions where an argument or the return value is treated as a pointer into the recorded process's memory.
///      Each read gives the argument (register or stack slot) or return value, an optional path of up to 3 hops (each hop reads a pointer
///      at an offset), and the offset of the final 8 byte value. Wider values such as a ColumnStoreObjectKey take several reads.
///
///      The read can be performed at the time of the call or the return. An at-return read of an argument uses the pointer captured at the
///      call, which is how out parameters are read.
///
///    - Event Writer/Publisher Functions
///
///      Extended events are excluded from the call tracing and tracked separately to allow the trace to be matched against the .xel events
///      tracked in general query execution.
///
///      An extended event is first published, then written into the buffer of each session collecting it by a shared writer. Entering a
///      publisher (an event's Publish) starts a pending event, and entering the shared writer (XE_BufferMgr::Reserve) while one is pending
///      records it as a published event: the publisher's position and the session buffer it was written to. Both are checked before
///      exclusion, so they are seen even though the extended events code is excluded from the tree. Published events are buffered and
///      passed back in chunks, then matched in order to the .xel events, giving each event its position in the trace.
///
///    The callbacks are:
///
///    - Call Log
///
///      The call log includes sequence, thread, registers (RCX, RDX, R8, R9), return value, and node index in the tree per call. Bit 32
///      of the thread value is set when the call returns.
///
///      Calls are buffered per function and instance. A call's row is written when the call is made, but its return value is only
///      filled in when it returns, so a chunk waits for its calls to return. The bridge flushes calls in chunks in several scenarios:
///
///          Normal - At 16,384 calls of the function, once none of them are in flight (InFlight == 0)
///          Forced - At 65,536 calls of the function, even with calls still in flight. Those calls lose their return value and
///                   at-return reads
///          Idle   - Every 262,144 calls across all functions, flushes each function with at least 256 calls and none in flight, so
///                   functions that never reach 16,384 don't hold memory until the end
///          End    - Everything left is flushed when the replay finishes
///
///      The call log is transposed to columns in the bridge as part of the flush process, then the columns are returned to the managed
///      code via a callback. Chunks are compressed after they have been returned to the managed code.
///
///    - Span Timeline
///
///      Start and end position, and instruction count, for each call, referenced to the call tree node index and the call's row in the
///      call log. A span is recorded when its frame is popped: on a return, when frames are left without returning (exception
///      unwinding), at a large gap, or at the end of the replay. Only spans that returned are flagged as returned. Frames that were
///      already running when the trace started have no call log row and are flagged as having an unknown start.
///
///      Spans arrive in the order their frames end, so children come before their parents. They are flushed every 65,536 spans and at
///      the end.
///
///    - Pointer Values
///
///      The values read by the pointer reads, with the function, instance and call they belong to, so they can be joined to the call
///      log. Nothing is returned if the pointer is zero or the trace has no value for the memory. Values are flushed every 65,536
///      values and at the end.
///
///    - Published Events
///
///      Each publish that reached a session buffer: the publisher's address, the session buffer (the writer's RCX), the position, the
///      thread's instruction count, the call tree node of the nearest recorded caller, and the thread. A publish written to two sessions
///      is passed back twice. Published events are flushed every 65,536 and at the end.
///
///    - Progress
///
///      Percentage complete for each thread, measured between the thread's first and last active position in the trace.
///
///    Pointers passed to the callbacks are only valid during the callback, so the data is copied before the callback returns.
///
///    Cancellation is through a pointer to an int that the bridge checks during the replay. Setting it to 1 interrupts the replay and
///    ReadCallTree returns Cancelled.
/// </remarks>
internal static class ReplayBridge
{
    private const int InstanceRegisterShift = 60;

    private const string Library = "InternalsViewer.Query.TimeTravelBridge.dll";

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ProgressCallback(uint thread, int percent);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void CallChunkCallback(ulong address, ulong instance, IntPtr columns, int calls);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void CallSpanCallback(IntPtr spans, int count);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void HookCallback(IntPtr hookEvent, IntPtr view, IntPtr session);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern int OpenTrace(string replayLibraryPath, string tracePath, out TraceHandle trace);

    [DllImport(Library)]
    public static extern int GetModuleCount(TraceHandle trace);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool GetModule(TraceHandle trace,
                                        int index,
                                        StringBuilder name,
                                        int nameLength,
                                        out ulong address,
                                        out ulong size);

    [DllImport(Library)]
    public static extern int ReadCallTree(TraceHandle trace,
                                          uint[] threadIds,
                                          int threadCount,
                                          ulong[] instanceMethods,
                                          int instanceMethodCount,
                                          ulong[] excludedFunctions,
                                          int excludedFunctionCount,
                                          HookFunction[] hookFunctions,
                                          int hookFunctionCount,
                                          CallChunkCallback? logCalls,
                                          CallSpanCallback? logSpans,
                                          HookCallback? onHook,
                                          ProgressCallback? progress,
                                          IntPtr cancel,
                                          out IntPtr tree);

    [DllImport(Library)]
    public static extern unsafe ulong ReadTraceMemory(IntPtr view, ulong address, byte* destination, ulong size);

    [DllImport(Library)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool WatchFrame(IntPtr session, uint thread, ulong frame);

    [DllImport(Library)]
    public static extern void WatchStack(IntPtr session, uint thread, ulong stackPointer);

    [DllImport(Library)]
    public static extern unsafe uint GetHookFrames(IntPtr session, uint thread, TraceHookFrame* frames, uint capacity);

    [DllImport(Library)]
    public static extern int GetCallNodeCount(IntPtr tree);

    [DllImport(Library)]
    public static extern void GetCallNodes(IntPtr tree, [Out] CallNode[] nodes, int count);

    [DllImport(Library)]
    public static extern void CloseCallTree(IntPtr tree);

    [DllImport(Library)]
    public static extern void CloseTrace(IntPtr trace);

    internal static ulong PackInstanceMethod(InstanceMethod method) => method.Address | ((ulong)method.Register << InstanceRegisterShift);
}
