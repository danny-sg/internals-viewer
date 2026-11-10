using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

internal sealed class FrameFunctionCache
{
    private Dictionary<(string Module, string Name), FrameFunction> Functions { get; } = [];

    public void Resolve(CallFrame frame, string symbol)
    {
        SymbolHelper.SplitOffset(symbol, out var name, out var offset);

        var key = (frame.Module.Name, name);

        if (!Functions.TryGetValue(key, out var function))
        {
            function = FrameFunction.Parse(frame.Module.Name, name);

            Functions[key] = function;
        }

        frame.Function = function;

        frame.Offset = offset;
    }
}
