using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Query.CallStack.TimeTravel.Native;

internal sealed class TimeTravelTraceHandle() : SafeHandleZeroOrMinusOneIsInvalid(ownsHandle: true)
{
    protected override bool ReleaseHandle()
    {
        TimeTravelBridge.CloseTrace(handle);

        return true;
    }
}
