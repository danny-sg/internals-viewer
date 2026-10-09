using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.CallStack.TimeTravel.Iterators;

public static class IteratorLifetimes
{
    public static IReadOnlyList<TimeTravelOperatorLifetime> Build(TimeTravelTimeline timeline,
                                                                  IReadOnlyList<ExecutionOperatorEvent> operators)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>();

        foreach (var operatorEvent in operators)
        {
            foreach (var instance in operatorEvent.Instances)
            {
                owners.TryAdd(instance, operatorEvent);
            }
        }

        var lifetimes = new List<TimeTravelOperatorLifetime>();

        if (owners.Count == 0)
        {
            return lifetimes;
        }

        foreach (var thread in timeline.Threads)
        {
            var calls = new Dictionary<ExecutionOperatorEvent, List<InstanceCall>>(ReferenceEqualityComparer.Instance);

            foreach (var row in thread.Rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    if (timeline.NodeOf(row.NodeAt(index))?.Frame is not { Instance: not 0 } frame
                        || !owners.TryGetValue(frame.Instance, out var owner))
                    {
                        continue;
                    }

                    if (!calls.TryGetValue(owner, out var spans))
                    {
                        spans = [];

                        calls[owner] = spans;
                    }

                    spans.Add(new InstanceCall(row.Span(TimeTravelTimelineAxis.Position, index),
                                               row.Span(TimeTravelTimelineAxis.Instructions, index)));
                }
            }

            var merged = calls.Select(c => (Operator: c.Key, Calls: Merged(c.Value))).ToList();

            var curves = timeline.InUseOwnedBy(thread.ThreadId, [.. merged.Select(m => m.Calls.Positions)]);

            for (var index = 0; index < merged.Count; index++)
            {
                var (owner, (positions, instructions)) = merged[index];

                lifetimes.Add(new TimeTravelOperatorLifetime(thread.ThreadId,
                                                             owner,
                                                             positions[0].Node,
                                                             positions,
                                                             instructions,
                                                             curves[index]));
            }
        }

        return lifetimes;
    }

    private static (TimeTravelTimelineSpan[] Positions, TimeTravelTimelineSpan[] Instructions) Merged(List<InstanceCall> calls)
    {
        calls.Sort((a, b) => a.Position.Start.CompareTo(b.Position.Start));

        var positions = new List<TimeTravelTimelineSpan>(calls.Count);

        var instructions = new List<TimeTravelTimelineSpan>(calls.Count);

        foreach (var (position, instruction) in calls)
        {
            if (positions.Count > 0 && position.Start <= positions[^1].End)
            {
                if (position.End > positions[^1].End)
                {
                    positions[^1] = positions[^1] with { End = position.End };
                }

                if (instruction.End > instructions[^1].End)
                {
                    instructions[^1] = instructions[^1] with { End = instruction.End };
                }

                continue;
            }

            positions.Add(position);

            instructions.Add(instruction);
        }

        return ([.. positions], [.. instructions]);
    }

    private readonly record struct InstanceCall(TimeTravelTimelineSpan Position, TimeTravelTimelineSpan Instructions);
}
