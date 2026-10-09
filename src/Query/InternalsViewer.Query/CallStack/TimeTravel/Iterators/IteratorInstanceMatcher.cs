using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.CallStack.TimeTravel.Iterators;

public static class IteratorInstanceMatcher
{
    private const int NameMatch = 10;

    private const int NameMismatch = -10;

    private const int RowMatch = 3;

    private const int SkippedInstance = 4;

    private const int SkippedPlanNode = 1;

    private const string Parallelism = "Parallelism";

    public static int Match(CallStackTree callStack, IReadOnlyList<EngineEvent> events)
    {
        var hierarchy = OperatorHierarchy.Build(events);

        var plan = new PlanShape(hierarchy);

        var matching = new Matching(plan);

        var assigned = new Dictionary<ExecutionOperatorEvent, List<IteratorInstance>>(ReferenceEqualityComparer.Instance);

        foreach (var (root, operatorEvent, choice) in AcceptedRoots(IteratorInstance.Build(callStack), plan, matching))
        {
            matching.Assign(root, operatorEvent, choice, assigned);
        }

        foreach (var (operatorEvent, instances) in assigned)
        {
            operatorEvent.EntryFrames = [.. instances.SelectMany(i => i.Entries).Distinct().OrderBy(e => e.Order)];
        }

        AssignStatements(plan, assigned);

        foreach (var operatorEvent in hierarchy.Operators)
        {
            operatorEvent.ExitFrames = ExitFrames(operatorEvent, hierarchy);
        }

        return assigned.Count;
    }

    private static IEnumerable<(IteratorInstance Root, ExecutionOperatorEvent Operator, Choice Choice)> AcceptedRoots(
        IReadOnlyList<IteratorInstance> roots,
        PlanShape plan,
        Matching matching)
    {
        var best = new Dictionary<ExecutionOperatorEvent, (IteratorInstance Root, Choice Choice)>(ReferenceEqualityComparer.Instance);

        foreach (var root in roots)
        {
            var candidates = plan.Operators
                                 .Where(o => plan.IsTop(o) || o.Name == Parallelism)
                                 .Select(o => (Operator: o, Choice: matching.Fit(root, o)))
                                 .Where(c => c.Choice.Score > 0)
                                 .OrderByDescending(c => c.Choice.Score)
                                 .ThenBy(c => c.Operator.PlanNodeIdentifier!.NodeId)
                                 .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            var (operatorEvent, choice) = candidates[0];

            if (operatorEvent.Name == Parallelism)
            {
                yield return (root, operatorEvent, choice);

                continue;
            }

            if (!best.TryGetValue(operatorEvent, out var current) || choice.Score > current.Choice.Score)
            {
                best[operatorEvent] = (root, choice);
            }
        }

        foreach (var (operatorEvent, (root, choice)) in best)
        {
            yield return (root, operatorEvent, choice);
        }
    }

    private static void AssignStatements(PlanShape plan,
                                         Dictionary<ExecutionOperatorEvent, List<IteratorInstance>> assigned)
    {
        foreach (var statement in plan.Statements)
        {
            if (statement.EntryFrames.Count > 0)
            {
                continue;
            }

            var entries = plan.Children(statement)
                              .Where(assigned.ContainsKey)
                              .SelectMany(o => o.EntryFrames)
                              .Select(e => e.Ancestors().Skip(1).FirstOrDefault(a => a.IsEntryFrameFor(statement.Name)))
                              .OfType<CallStackNode>()
                              .Distinct()
                              .OrderBy(e => e.Order)
                              .ToList();

            if (entries.Count > 0)
            {
                statement.EntryFrames = entries;
            }
        }
    }

    private static List<CallStackNode> ExitFrames(ExecutionOperatorEvent operatorEvent, OperatorHierarchy hierarchy)
        => [.. hierarchy.Descendants(operatorEvent)
                        .SelectMany(d => d.EntryFrames)
                        .Where(e => !operatorEvent.EntryFrames.Contains(e))
                        .Distinct()];

    private enum InstanceKind
    {
        Operator,
        Wrapper,
        Unknown
    }

    private enum ChoiceKind
    {
        Direct,
        SkipPlanNode,
        SkipInstance
    }

    private sealed record Choice(int Score,
                                 ChoiceKind Kind,
                                 IReadOnlyList<(IteratorInstance Instance, ExecutionOperatorEvent Operator)> Pairs);

    private sealed class PlanShape(OperatorHierarchy hierarchy)
    {
        public IReadOnlyList<ExecutionOperatorEvent> Operators { get; }
            = [.. hierarchy.Operators.Where(o => !IsStatement(o))];

        public IReadOnlyList<ExecutionOperatorEvent> Statements { get; } = [.. hierarchy.Operators.Where(IsStatement)];

        private Dictionary<ExecutionOperatorEvent, List<ExecutionOperatorEvent>> ChildrenByOperator { get; }
            = new(ReferenceEqualityComparer.Instance);

        public List<ExecutionOperatorEvent> Children(ExecutionOperatorEvent operatorEvent)
        {
            if (!ChildrenByOperator.TryGetValue(operatorEvent, out var children))
            {
                children = [.. hierarchy.Children(operatorEvent).OrderBy(c => c.PlanNodeIdentifier!.NodeId)];

                ChildrenByOperator[operatorEvent] = children;
            }

            return children;
        }

        public bool IsTop(ExecutionOperatorEvent operatorEvent)
            => hierarchy.Parent(operatorEvent) is null or { PlanNodeIdentifier.NodeId: < 0 };

        private static bool IsStatement(ExecutionOperatorEvent operatorEvent) => operatorEvent.PlanNodeIdentifier!.NodeId < 0;
    }

    private sealed class Matching(PlanShape plan)
    {
        private Dictionary<(IteratorInstance, PlanNodeIdentifier), Choice> Choices { get; } = new();

        public Choice Fit(IteratorInstance instance, ExecutionOperatorEvent operatorEvent)
        {
            if (Choices.TryGetValue((instance, operatorEvent.PlanNodeIdentifier!), out var choice))
            {
                return choice;
            }

            choice = Direct(instance, operatorEvent);

            var planChildren = plan.Children(operatorEvent);

            if (planChildren.Count == 1
                && Fit(instance, planChildren[0]) is var skipPlan
                && skipPlan.Score - SkippedPlanNode > choice.Score)
            {
                choice = new Choice(skipPlan.Score - SkippedPlanNode,
                                    ChoiceKind.SkipPlanNode,
                                    [(instance, planChildren[0])]);
            }

            if (instance.Kind == InstanceKind.Unknown
                && instance.EffectiveChildren is [var only]
                && Fit(only, operatorEvent) is var skipInstance
                && skipInstance.Score - SkippedInstance > choice.Score)
            {
                choice = new Choice(skipInstance.Score - SkippedInstance,
                                    ChoiceKind.SkipInstance,
                                    [(only, operatorEvent)]);
            }

            Choices[(instance, operatorEvent.PlanNodeIdentifier!)] = choice;

            return choice;
        }

        public void Assign(IteratorInstance instance,
                           ExecutionOperatorEvent operatorEvent,
                           Choice choice,
                           Dictionary<ExecutionOperatorEvent, List<IteratorInstance>> assigned)
        {
            if (choice.Kind == ChoiceKind.Direct)
            {
                if (!assigned.TryGetValue(operatorEvent, out var instances))
                {
                    instances = [];

                    assigned[operatorEvent] = instances;
                }

                instances.Add(instance);
            }

            foreach (var (childInstance, childOperator) in choice.Pairs)
            {
                Assign(childInstance, childOperator, Fit(childInstance, childOperator), assigned);
            }
        }

        private Choice Direct(IteratorInstance instance, ExecutionOperatorEvent operatorEvent)
        {
            var (score, pairs) = Align(instance.EffectiveChildren, plan.Children(operatorEvent));

            score += NameScore(instance, operatorEvent) + RowScore(instance, operatorEvent);

            return new Choice(score, ChoiceKind.Direct, pairs);
        }

        private (int Score, List<(IteratorInstance, ExecutionOperatorEvent)> Pairs) Align(
            IReadOnlyList<IteratorInstance> instances,
            IReadOnlyList<ExecutionOperatorEvent> operators)
        {
            var scores = new int[instances.Count + 1, operators.Count + 1];

            for (var i = 1; i <= instances.Count; i++)
            {
                scores[i, 0] = scores[i - 1, 0] - SkippedInstance;
            }

            for (var j = 1; j <= operators.Count; j++)
            {
                scores[0, j] = scores[0, j - 1] - SkippedPlanNode;
            }

            for (var i = 1; i <= instances.Count; i++)
            {
                for (var j = 1; j <= operators.Count; j++)
                {
                    var best = Math.Max(scores[i - 1, j] - SkippedInstance, scores[i, j - 1] - SkippedPlanNode);

                    if (Fit(instances[i - 1], operators[j - 1]) is { Score: > 0 } fit)
                    {
                        best = Math.Max(best, scores[i - 1, j - 1] + fit.Score);
                    }

                    scores[i, j] = best;
                }
            }

            var pairs = new List<(IteratorInstance, ExecutionOperatorEvent)>();

            var row = instances.Count;

            var column = operators.Count;

            while (row > 0 && column > 0)
            {
                if (Fit(instances[row - 1], operators[column - 1]) is { Score: > 0 } fit
                    && scores[row, column] == scores[row - 1, column - 1] + fit.Score)
                {
                    pairs.Add((instances[row - 1], operators[column - 1]));

                    row--;

                    column--;
                }
                else if (scores[row, column] == scores[row - 1, column] - SkippedInstance)
                {
                    row--;
                }
                else
                {
                    column--;
                }
            }

            pairs.Reverse();

            return (scores[instances.Count, operators.Count], pairs);
        }

        private static int NameScore(IteratorInstance instance, ExecutionOperatorEvent operatorEvent) => instance.Kind switch
        {
            InstanceKind.Operator when instance.Entries.Any(e => e.IsEntryFrameFor(operatorEvent.Name)) => NameMatch,
            InstanceKind.Operator => NameMismatch,
            _ => 0
        };

        private static int RowScore(IteratorInstance instance, ExecutionOperatorEvent operatorEvent)
        {
            if (operatorEvent.RowsProcessed <= 0 || instance.GetRowCalls == 0)
            {
                return 0;
            }

            var excess = instance.GetRowCalls - operatorEvent.RowsProcessed;

            return excess >= 0 && excess <= Math.Max(instance.OpenCalls, 1) ? RowMatch : 0;
        }
    }

    private sealed class IteratorInstance(ulong address)
    {
        private ulong Address { get; } = address;

        public List<CallStackNode> Entries { get; } = [];

        private IteratorInstance? Parent { get; set; }

        private List<IteratorInstance> Children { get; } = [];

        public InstanceKind Kind { get; private set; } = InstanceKind.Unknown;

        public long GetRowCalls { get; private set; }

        public long OpenCalls { get; private set; }

        private int Order => Entries[0].Order;

        public IReadOnlyList<IteratorInstance> EffectiveChildren { get; private set; } = [];

        public static IReadOnlyList<IteratorInstance> Build(CallStackTree callStack)
        {
            var instances = new Dictionary<ulong, IteratorInstance>();

            var callers = new Dictionary<IteratorInstance, (int Order, IteratorInstance? Context)>();

            var pending = new Stack<(CallStackNode Node, IteratorInstance? Context)>();

            foreach (var child in callStack.Root.ChildNodes)
            {
                pending.Push((child, null));
            }

            while (pending.Count > 0)
            {
                var (node, context) = pending.Pop();

                if (node.Frame is { Instance: not 0 } frame && frame.Instance != context?.Address)
                {
                    if (!instances.TryGetValue(frame.Instance, out var instance))
                    {
                        instance = new IteratorInstance(frame.Instance);

                        instances[frame.Instance] = instance;
                    }

                    if (!callers.TryGetValue(instance, out var caller) || node.Order < caller.Order)
                    {
                        callers[instance] = (node.Order, context);
                    }

                    instance.AddEntry(node);

                    context = instance;
                }

                foreach (var child in node.ChildNodes)
                {
                    pending.Push((child, context));
                }
            }

            foreach (var instance in instances.Values)
            {
                instance.Entries.Sort((x, y) => x.Order.CompareTo(y.Order));

                instance.Parent = callers[instance].Context;

                instance.Parent?.Children.Add(instance);
            }

            foreach (var instance in instances.Values)
            {
                instance.EffectiveChildren = Flatten(instance.Children);
            }

            return Flatten(instances.Values.Where(i => i.Parent is null));
        }

        private static List<IteratorInstance> Flatten(IEnumerable<IteratorInstance> instances)
            => [.. instances.SelectMany(i => i.Kind == InstanceKind.Wrapper ? Flatten(i.Children) : [i]).OrderBy(i => i.Order)];

        private void AddEntry(CallStackNode node)
        {
            Entries.Add(node);

            var kind = node switch
            {
                { IsOperatorBoundary: true } => InstanceKind.Operator,
                { HasOperator: true } => InstanceKind.Wrapper,
                _ => InstanceKind.Unknown
            };

            if (kind < Kind)
            {
                Kind = kind;
            }

            switch (node.Frame?.Resolved?.MethodName)
            {
                case "GetRow" or "GetRowOrReQualifyHelper":
                {
                    GetRowCalls += node.Calls;

                    break;
                }
                case "Open":
                {
                    OpenCalls += node.Calls;

                    break;
                }
            }
        }
    }
}
