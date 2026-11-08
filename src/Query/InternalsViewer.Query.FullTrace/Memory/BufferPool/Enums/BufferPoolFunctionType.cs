namespace InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;

internal enum BufferPoolFunctionType : byte
{
    Get,
    PostRead,
    LatchRelease
}
