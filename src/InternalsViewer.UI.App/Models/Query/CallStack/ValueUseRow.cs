using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class ValueUseRow(TimeTravelValueUse use)
{
    public TimeTravelValueUse Use { get; } = use;

    public string Function => Use.First.Node?.Symbol is { Length: > 0 } symbol ? symbol : $"0x{Use.Address:X}";

    public string Location => Use.Location;

    public string Calls => Use.Calls == 1 ? "1 Call" : $"{Use.Calls:N0} Calls";

    public string FirstLabel => Use.Calls == 1 ? "Show Call" : "First Call";

    public bool HasLast => Use.Calls > 1 && Use.Last.Node is not null;

    public bool CanShow => Use.First.Node is not null;
}
