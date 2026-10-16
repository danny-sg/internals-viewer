using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.CustomHooks;

internal sealed class CustomHook(IEnumerable<(ulong Address, CustomHookDefinition Definition)> hooks)
    : ArgumentHook<CustomHookDefinition>(hooks)
{
    private const int MaximumLength = 0x10000;

    private List<CustomHookCapture> Captures { get; } = [];

    public CustomHookStore Build() => new([.. Captures]);

    protected override void Read(ITraceReplay replay, in TraceHookCall call, CustomHookDefinition definition, ulong pointer)
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
}
