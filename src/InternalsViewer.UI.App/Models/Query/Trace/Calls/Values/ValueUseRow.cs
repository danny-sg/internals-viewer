using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Values;

public sealed class ValueUseRow(TraceValueUse use, ulong value = 0, CallFrame? frame = null)
{
    public TraceValueUse Use { get; } = use;

    public ulong Value { get; } = value;

    public string Function => frame is null ? $"0x{Use.FunctionAddress:X}" : frame.QualifiedName;

    public string Location => Use.Location;

    public string Calls => CountText.GetCalls(Use.CallCount);
}
