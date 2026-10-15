using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.CustomHooks;

internal sealed class CustomHook(IEnumerable<(ulong Address, CustomHookDefinition Definition)> hooks) : ITraceHook
{
    private const int MaximumLength = 0x10000;

    public IEnumerable<TraceHookFunction> Functions
        => Definitions.Select(d => new TraceHookFunction(d.Key, d.Value.Any(h => h.IsReadAtReturn)
                                                                    ? TraceHookEvents.Entry | TraceHookEvents.Return
                                                                    : TraceHookEvents.Entry));

    private Dictionary<ulong, CustomHookDefinition[]> Definitions { get; }
        = hooks.GroupBy(h => h.Address).ToDictionary(g => g.Key, g => g.Select(h => h.Definition).ToArray());

    private Dictionary<ulong, CustomHookStart> Starts { get; } = [];

    private List<CustomHookCapture> Captures { get; } = [];

    public CustomHookIndex Build() => new([.. Captures]);

    public void OnEntry(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Definitions.TryGetValue(call.Function, out var definitions))
        {
            return;
        }

        ulong[]? stackArguments = null;

        var isReadAtReturn = false;

        foreach (var definition in definitions)
        {
            if (definition.Register is >= HookArguments.RegisterCount and < HookArguments.SlotCount)
            {
                stackArguments ??= HookArguments.LoadStack(replay, call.StackPointer, call.ReturnAddress);
            }

            if (definition.IsReadAtReturn)
            {
                isReadAtReturn = true;

                continue;
            }

            Capture(replay, call, definition, GetArgument(call.Arguments, stackArguments, definition.Register));
        }

        if (isReadAtReturn)
        {
            Starts[call.Id] = new CustomHookStart([.. call.Arguments], stackArguments);
        }
    }

    public void OnReturn(in TraceHookCall call, ITraceReplay replay)
    {
        if (!Starts.Remove(call.Id, out var start) || !Definitions.TryGetValue(call.Function, out var definitions))
        {
            return;
        }

        foreach (var definition in definitions)
        {
            if (!definition.IsReadAtReturn)
            {
                continue;
            }

            var pointer = definition.Register == HookArguments.ReturnRegister
                ? call.ReturnValue
                : GetArgument(start.Arguments, start.StackArguments, definition.Register);

            Capture(replay, call, definition, pointer);
        }
    }

    public void OnAbandoned(in TraceHookCall call) => Starts.Remove(call.Id);

    private static ulong GetArgument(ReadOnlySpan<ulong> arguments, ulong[]? stackArguments, int register)
        => register switch
        {
            >= 0 and < HookArguments.RegisterCount => arguments[register],
            < HookArguments.SlotCount when stackArguments is not null => stackArguments[register - HookArguments.RegisterCount],
            _ => 0
        };

    private void Capture(ITraceReplay replay, in TraceHookCall call, CustomHookDefinition definition, ulong pointer)
    {
        if (pointer == 0)
        {
            return;
        }

        var address = pointer + (ulong)(long)definition.Offset;

        var data = new byte[Math.Clamp(definition.Length, 0, MaximumLength)];

        var read = replay.Read(address, data);

        Captures.Add(new CustomHookCapture(definition.Name,
                                           call.Function,
                                           call.Instance,
                                           (int)call.Call,
                                           call.Id,
                                           call.Thread,
                                           address,
                                           read == data.Length ? data : data[..read]));
    }

    private sealed record CustomHookStart(ulong[] Arguments, ulong[]? StackArguments);
}
