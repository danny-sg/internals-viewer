using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;

namespace InternalsViewer.Query.FullTrace.PublishedEvents;

internal sealed class PublishedEventHook(PublishedEventFunctions functions) : ITraceHook
{
    private const int SessionBufferArgument = 0;

    public IEnumerable<TraceHookFunction> Functions => IsPublisher.Keys.Select(f => new TraceHookFunction(f, TraceHookEvents.Call));

    private Dictionary<ulong, bool> IsPublisher { get; } = GetFunctions(functions);

    private Dictionary<uint, List<PendingPublish>> Publishing { get; } = [];

    private int PendingCount { get; set; }

    private List<PublishedEvent> Events { get; } = [];

    public PublishedEvent[] Build() => [.. Events];

    public void OnCall(in TraceHookCall call, ITraceReplay replay)
    {
        if (!IsPublisher.TryGetValue(call.Function, out var isPublisher))
        {
            return;
        }

        if (isPublisher)
        {
            BeginPublish(call, replay);
        }
        else if (PendingCount > 0)
        {
            RecordWrite(call.Thread, call.StackPointer, call.Arguments[SessionBufferArgument]);
        }
    }

    public void OnStackUnwound(in TraceHookCall call) => EndPublishes(call.Thread, call.StackPointer);

    private static Dictionary<ulong, bool> GetFunctions(PublishedEventFunctions functions)
    {
        var types = new Dictionary<ulong, bool>();

        foreach (var publisher in functions.Publishers)
        {
            types[publisher.Address] = true;
        }

        foreach (var writer in functions.Writers)
        {
            types[writer] = false;
        }

        return types;
    }

    private void BeginPublish(in TraceHookCall call, ITraceReplay replay)
    {
        EndPublishes(call.Thread, call.StackPointer);

        if (!Publishing.TryGetValue(call.Thread, out var publishing))
        {
            publishing = [];

            Publishing[call.Thread] = publishing;
        }

        publishing.Add(new PendingPublish(call.Function, call.StackPointer, call.Position, call.Instructions, call.Node, 0));

        PendingCount++;

        replay.WatchStack(call.Thread, call.StackPointer);
    }

    private void EndPublishes(uint thread, ulong stackPointer)
    {
        if (!Publishing.TryGetValue(thread, out var publishing))
        {
            return;
        }

        while (publishing.Count > 0 && publishing[^1].StackPointer <= stackPointer)
        {
            publishing.RemoveAt(publishing.Count - 1);

            PendingCount--;
        }
    }

    private void RecordWrite(uint thread, ulong stackPointer, ulong buffer)
    {
        EndPublishes(thread, stackPointer);

        if (!Publishing.TryGetValue(thread, out var publishing) || publishing.Count == 0 || publishing[^1].SessionBuffer == buffer)
        {
            return;
        }

        var publish = publishing[^1] with { SessionBuffer = buffer };

        publishing[^1] = publish;

        Events.Add(new PublishedEvent(publish.Publisher,
                                      buffer,
                                      publish.Position,
                                      publish.Instructions,
                                      publish.Node,
                                      thread));
    }

    private readonly record struct PendingPublish(ulong Publisher,
                                                  ulong StackPointer,
                                                  TracePosition Position,
                                                  ulong Instructions,
                                                  int Node,
                                                  ulong SessionBuffer);
}
