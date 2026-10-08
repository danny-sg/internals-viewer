using System.Runtime.InteropServices;
using System.Text;

namespace InternalsViewer.Query.CallStack.TimeTravel;

internal static class TimeTravelBridge
{
    private const string Library = "InternalsViewer.Query.TimeTravelBridge.dll";

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ProgressCallback(int percent);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void CallChunkCallback(ulong address, ulong instance, IntPtr columns, int calls);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern int OpenTrace(string replayLibraryPath, string tracePath, out TimeTravelTraceHandle trace);

    [DllImport(Library)]
    public static extern int GetModuleCount(TimeTravelTraceHandle trace);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool GetModule(TimeTravelTraceHandle trace,
                                        int index,
                                        StringBuilder name,
                                        int nameLength,
                                        out ulong address,
                                        out ulong size);

    [DllImport(Library)]
    public static extern int ReadCallTree(TimeTravelTraceHandle trace,
                                          uint[] threadIds,
                                          int threadCount,
                                          ulong[] instanceMethods,
                                          int instanceMethodCount,
                                          int activitySlices,
                                          CallChunkCallback? logCalls,
                                          ProgressCallback? progress,
                                          IntPtr cancel,
                                          out IntPtr tree);

    [DllImport(Library)]
    public static extern int GetCallNodeCount(IntPtr tree);

    [DllImport(Library)]
    public static extern void GetCallNodes(IntPtr tree, [Out] TimeTravelCallNode[] nodes, int count);

    [DllImport(Library)]
    public static extern int GetCallActivityCount(IntPtr tree);

    [DllImport(Library)]
    public static extern void GetCallActivity(IntPtr tree, [Out] TimeTravelCallActivity[] activity, int count);

    [DllImport(Library)]
    public static extern void CloseCallTree(IntPtr tree);

    [DllImport(Library)]
    public static extern void CloseTrace(IntPtr trace);
}
