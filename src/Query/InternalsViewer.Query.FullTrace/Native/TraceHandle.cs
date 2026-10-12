using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Query.FullTrace.Native;

internal sealed class TraceHandle() : SafeHandleZeroOrMinusOneIsInvalid(ownsHandle: true)
{
    protected override bool ReleaseHandle()
    {
        ReplayBridge.CloseTrace(handle);

        return true;
    }
}
