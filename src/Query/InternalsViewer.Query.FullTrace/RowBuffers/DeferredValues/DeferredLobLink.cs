using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

public readonly record struct DeferredLobLink(int Offset, PageAddress Page, int Slot);
