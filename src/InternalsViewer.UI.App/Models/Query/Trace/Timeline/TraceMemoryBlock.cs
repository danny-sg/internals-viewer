using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record TraceMemoryBlock(int Block,
                                      string Label,
                                      ulong Address,
                                      int Size,
                                      TracePoint From,
                                      TracePoint? To,
                                      TraceMemoryWrites Writes,
                                      bool IsBuffer = false);
