using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceMemoryBlock(int Block,
                                      string Label,
                                      ulong Address,
                                      int Size,
                                      TracePoint From,
                                      TracePoint? To,
                                      TraceMemoryWrites Writes,
                                      bool IsBuffer = false,
                                      PageAddress[]? Pages = null,
                                      TraceMemoryWrites? Reads = null);
