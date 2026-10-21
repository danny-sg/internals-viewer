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
/// The bridge uses a hook mechanism to pass control back to managed code using the ITraceHook interface. The hooks are registered with a
/// set of addresses they are interested in. When the bridge reaches an address with a hook it will dispatch a callback for the hook on the
/// same thread and control is passed to this managed project. It can then read memory including pointer following to extract the
/// information it needs. When the hook returns control is passed back to the bridge and the replay continues.
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
///    - Hook Functions
///
///      Each hook function is an address to watch, a mask of the events to fire (Call, Entry, Return), and a hook id passed back with
///      each event so the managed side can route it. One event is dispatched per address; the managed side fans out to every hook
///      registered for that address.
///
///      The hook callback receives a HookEvent with one of six types:
///
///          Call         - A call instruction to the address was found, before any tracking. No call id or instance yet
///          Entry        - The call was tracked, logged and added to the call tree, with its call id, log row and instance
///          Return       - A tracked frame was popped with a confirmed return, including the return value (RAX)
///          Abandoned    - A tracked frame was popped without a confirmed return (e.g. exception unwinding), no return value
///          FrameEnded   - A frame marked with WatchFrame left the stack, whether it returned or not. Sent to every hook
///          StackUnwound - The stack pointer passed a stack pointer value registered with WatchStack. Sent to every hook
///
///      The callback also receives a view handle for ReadTraceMemory, and a session handle for WatchFrame, WatchStack and
///      GetHookFrames. Both handles are only valid for the duration of the callback.
///
///    The callbacks are:
///
///    - Call Log
///
///      The call log has one row per call with start sequence and steps, thread, registers (RCX, RDX, R8, R9), return value, node
///      index in the tree, and end sequence and steps. Bit 32 of the thread value is set when the call returns.
///
///      Calls are buffered per function and instance. A call's row is written when the call is made, but its return value and end
///      position are only filled in when it returns, so a chunk waits for its calls to return. The bridge flushes calls in chunks in
///      several scenarios:
///
///          Normal - At 16,384 calls of the function, once none of them are in flight (InFlight == 0)
///          Forced - At 65,536 calls of the function, even with calls still in flight. Those calls are flushed without their return
///                   value and end position, which are reported later through the Call End callback when the call returns
///          Idle   - Every 262,144 calls across all functions, flushes each function with at least 256 calls and none in flight, so
///                   functions that never reach 16,384 don't hold memory until the end
///          End    - Everything left is flushed when the replay finishes
///
///      The call log is transposed to columns in the bridge as part of the flush process, then the columns are returned to the managed
///      code via a callback. Chunks are compressed after they have been returned to the managed code.
///
///    - Call End
///
///      When a call ends after its chunk has already been force flushed, the Call End callback reports the function, instance, call
///      row, end position, return value, and whether the call returned, so the managed side can patch the already received chunk.
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
///    - Hook
///
///      Fired for each hook event as described under Hook Functions above.
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
    public delegate void CallEndCallback(ulong address,
                                         ulong instance,
                                         uint callIndex,
                                         ulong endSequence,
                                         ulong endSteps,
                                         ulong returnValue,
                                         uint returned);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void CallSpanCallback(IntPtr spans, int count);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void HookCallback(IntPtr hookEvent, IntPtr view, IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int StubCallback(IntPtr view, ulong address, IntPtr registers, IntPtr target);

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
                                          CallEndCallback? logCallEnds,
                                          CallSpanCallback? logSpans,
                                          HookCallback? onHook,
                                          StubCallback? resolveStub,
                                          ProgressCallback? progress,
                                          IntPtr cancel,
                                          out IntPtr tree);

    [DllImport(Library)]
    public static extern unsafe ulong ReadTraceMemory(IntPtr view, ulong address, byte* destination, ulong size);

    [DllImport(Library)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool WatchFrame(IntPtr session, uint thread, ulong frame);

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
