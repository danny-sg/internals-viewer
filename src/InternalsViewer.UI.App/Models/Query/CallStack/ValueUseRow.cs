using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueUseRow(TraceValueUse use, ulong value = 0, CallstackFrame? frame = null)
{
    public TraceValueUse Use { get; } = use;

    public ulong Value { get; } = value;

    public string Function => frame is null ? $"0x{Use.Address:X}" : QualifiedSymbol.Of(frame);

    public string Location => Use.Location;

    public string Calls => Use.Calls == 1 ? "1 Call" : $"{Use.Calls:N0} Calls";
}
