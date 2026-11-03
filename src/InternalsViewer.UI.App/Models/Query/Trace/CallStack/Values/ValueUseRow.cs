using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Values;

public sealed class ValueUseRow(TraceValueUse use, ulong value = 0, CallstackFrame? frame = null)
{
    public TraceValueUse Use { get; } = use;

    public ulong Value { get; } = value;

    public string Function => frame is null ? $"0x{Use.FunctionAddress:X}" : QualifiedSymbol.Of(frame);

    public string Location => Use.Location;

    public string Calls => CountText.GetCalls(Use.Calls);
}
