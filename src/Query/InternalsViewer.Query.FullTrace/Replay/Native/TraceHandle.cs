using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Query.FullTrace.Replay.Native;

internal sealed class TraceHandle() : SafeHandleZeroOrMinusOneIsInvalid(ownsHandle: true)
{
    protected override bool ReleaseHandle()
    {
        ReplayBridge.CloseTrace(handle);

        return true;
    }
}
