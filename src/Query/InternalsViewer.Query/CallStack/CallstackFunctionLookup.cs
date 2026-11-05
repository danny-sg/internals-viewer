using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.CallStack;

internal sealed class CallstackFunctionLookup
{
    private Dictionary<(string Module, string Name), CallstackFunction> Functions { get; } = [];

    public void Resolve(CallstackFrame frame, string symbol)
    {
        SymbolsHelper.SplitOffset(symbol, out var name, out var offset);

        var key = (frame.Module.Name, name);

        if (!Functions.TryGetValue(key, out var function))
        {
            function = CallstackFunction.Parse(frame.Module.Name, name);

            Functions[key] = function;
        }

        frame.Function = function;

        frame.Offset = offset;
    }
}
