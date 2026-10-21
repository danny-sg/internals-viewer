using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed record TraceMemoryBlock(int Block,
                                      string Label,
                                      ulong Address,
                                      int Size,
                                      TracePoint From,
                                      TracePoint? To,
                                      TraceMemoryWrite[] Writes);
