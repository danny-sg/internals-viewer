using InternalsViewer.Query.FullTrace.Stubs.Enums;

namespace InternalsViewer.Query.FullTrace.Stubs;

internal readonly record struct StubOperation(StubOperationType Type, int Destination, int Source, long Displacement, ulong Address)
{
    public const int AbsoluteSource = -1;

    public bool IsAbsolute => Source == AbsoluteSource;
}
