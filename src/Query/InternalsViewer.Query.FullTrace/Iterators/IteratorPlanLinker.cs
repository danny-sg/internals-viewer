using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class IteratorPlanLinker
{
    private const string RowFactoryClass = "CXte";

    private const string RowFactoryMethod = "QScanGet";

    private const string BatchClass = "CBpXte";

    private const string BatchFactoryMethod = "CreateBatchIterators";

    private const string BatchIteratorMethod = "PbpqsGet";

    private const int FirstRegister = 0;

    private const int SecondRegister = 1;

    private const int PlanObjectRegister = 2;

    private const int NameMatch = 10;

    private const int NameMismatch = -10;

    private const int SkippedPlanNode = 1;

    private const int SkippedPlanObject = 4;

    public static IteratorPlanLink Link(TraceCallStore callStore, IReadOnlyList<CallStackNode?> nodes, IEnumerable<EngineEvent> events)
    {
        List<LoggedCall> factories = [.. LoggedCalls(callStore, nodes, IsRowFactory, FirstRegister),
                                      .. LoggedCalls(callStore, nodes, IsBatchFactory, SecondRegister)];

        if (factories.Count == 0)
        {
            return IteratorPlanLink.Empty;
        }

        var factoryNodes = factories.ToLookup<LoggedCall, CallStackNode>(f => f.Node, ReferenceEqualityComparer.Instance);

        var batchIterators = BatchIterators(LoggedCalls(callStore, nodes, IsBatchIterator, FirstRegister), factoryNodes);

        var constructors = LoggedCalls(callStore, nodes, IsIteratorConstructor, FirstRegister);

        var objects = PlanObject.Build(factories, factoryNodes, Operators(nodes, constructors, batchIterators));

        var roots = objects.Values.Where(o => o.Parent is null).Order(PlanObject.ByFirstCall).ToList();

        var plan = new PlanShape(OperatorHierarchy.Build(events));

        var matching = new Matching(plan);

        var planObjects = new Dictionary<ulong, PlanNodeIdentifier>();

        foreach (var (root, operatorEvent, choice) in AcceptedRoots(roots, plan, matching))
        {
            matching.Assign(root, operatorEvent, choice, planObjects);
        }

        var instances = Instances(factories, constructors, batchIterators, objects, planObjects);

        foreach (var (wrapper, node) in Wrappers(nodes, instances))
        {
            instances[wrapper] = node;
        }

        foreach (var iterator in batchIterators)
        {
            if (planObjects.TryGetValue(iterator.PlanObjectId, out var node))
            {
                planObjects.TryAdd(iterator.BatchObjectId, node);
            }
        }

        return new IteratorPlanLink(planObjects, instances);
    }

    private static List<LoggedCall> LoggedCalls(TraceCallStore callStore,
                                                IReadOnlyList<CallStackNode?> nodes,
                                                Func<CallStackNode, bool> predicate,
                                                int objectRegister)
    {
        var calls = new List<LoggedCall>();

        var functions = nodes.OfType<CallStackNode>().Where(predicate).Select(n => (n.Frame!.Address, n.Frame.Instance)).Distinct();

        foreach (var (address, instance) in functions)
        {
            if (callStore.GetCallList(address, instance) is not { } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (call.Node >= 0 && call.Node < nodes.Count && nodes[call.Node] is { } node)
                {
                    calls.Add(new LoggedCall(node, index, call, objectRegister));
                }
            }
        }

        return calls;
    }

    private static List<BatchIterator> BatchIterators(IReadOnlyList<LoggedCall> getters, ILookup<CallStackNode, LoggedCall> factoryNodes)
    {
        var iterators = new List<BatchIterator>();

        foreach (var getter in getters)
        {
            if (getter.Call is { Returned: true, ReturnValue: not 0 } && GetParent(getter, factoryNodes) is { } factory)
            {
                iterators.Add(new BatchIterator(getter.Call.ReturnValue, getter.Object, factory.Object));
            }
        }

        return iterators;
    }

    private static Dictionary<ulong, List<GlobPattern>> Operators(IReadOnlyList<CallStackNode?> nodes,
                                                                  IReadOnlyList<LoggedCall> constructors,
                                                                  IReadOnlyList<BatchIterator> batchIterators)
    {
        var operators = new Dictionary<ulong, List<GlobPattern>>();

        foreach (var constructor in constructors)
        {
            AddOperators(operators, constructor.PlanObjectId, constructor.Node.Frame?.Resolved?.PlanOperator ?? []);
        }

        var frames = nodes.OfType<CallStackNode>().Where(n => n.Frame is { Instance: not 0 }).ToLookup(n => n.Frame!.Instance);

        foreach (var iterator in batchIterators)
        {
            foreach (var node in frames[iterator.Instance])
            {
                AddOperators(operators, iterator.PlanObjectId, node.Frame?.Resolved?.PlanOperator ?? []);
            }
        }

        return operators;
    }

    private static void AddOperators(Dictionary<ulong, List<GlobPattern>> operators, ulong planObject, IReadOnlyList<GlobPattern> patterns)
    {
        if (!operators.TryGetValue(planObject, out var list))
        {
            list = [];

            operators[planObject] = list;
        }

        list.AddRange(patterns.Where(p => !list.Contains(p)));
    }

    private static LoggedCall? GetParent(LoggedCall call, ILookup<CallStackNode, LoggedCall> factoryNodes)
    {
        for (var ancestor = call.Node.Parent; ancestor is { IsRoot: false }; ancestor = ancestor.Parent)
        {
            if (!factoryNodes.Contains(ancestor))
            {
                continue;
            }

            var parent = factoryNodes[ancestor].Where(f => f.Thread == call.Thread && f.FromPosition <= call.FromPosition)
                                               .MaxBy(f => (f.FromPosition, f.Index));

            if (parent is not null)
            {
                return parent;
            }
        }

        return null;
    }

    private static List<(ulong Wrapper, PlanNodeIdentifier Node)> Wrappers(IReadOnlyList<CallStackNode?> nodes,
                                                                          Dictionary<ulong, PlanNodeIdentifier> instances)
    {
        var wrappers = new List<(ulong Wrapper, PlanNodeIdentifier Node)>();

        var unlinked = nodes.OfType<CallStackNode>()
                            .Where(n => n.Frame is { Instance: not 0 } frame && !instances.ContainsKey(frame.Instance) && IsWrapper(n))
                            .ToHashSet<CallStackNode>(ReferenceEqualityComparer.Instance)
                            .GroupBy(n => n.Frame!.Instance);

        foreach (var wrapper in unlinked)
        {
            var wrapped = wrapper.SelectMany(n => n.ChildNodes)
                                 .Select(c => c.Frame is { Instance: not 0 } frame ? instances.GetValueOrDefault(frame.Instance) : null)
                                 .OfType<PlanNodeIdentifier>()
                                 .Distinct()
                                 .ToList();

            if (wrapped is [var node])
            {
                wrappers.Add((wrapper.Key, node));
            }
        }

        return wrappers;
    }

    private static bool IsWrapper(CallStackNode node) => node.HasOperator && node.Frame?.Resolved?.SymbolCategory is SymbolCategory.Tracing;

    private static bool IsRowFactory(CallStackNode node)
        => node.Frame?.Resolved is { ClassName: { } className, MethodName: { } methodName }
           && className.StartsWith(RowFactoryClass, StringComparison.Ordinal)
           && methodName.StartsWith(RowFactoryMethod, StringComparison.Ordinal);

    private static bool IsBatchFactory(CallStackNode node)
        => node.Frame?.Resolved is { ClassName: BatchClass, MethodName: BatchFactoryMethod };

    private static bool IsBatchIterator(CallStackNode node)
        => node.Frame?.Resolved is { ClassName: { } className, MethodName: BatchIteratorMethod }
           && className.StartsWith(BatchClass, StringComparison.Ordinal);

    internal static bool IsIteratorConstructor(CallStackNode node)
        => node is { IsConstructor: true, Frame.Resolved.SymbolCategory: SymbolCategory.QueryOperator or SymbolCategory.PhysicalOperator };

    private static IEnumerable<(PlanObject Root, ExecutionOperatorEvent Operator, Choice Choice)> AcceptedRoots(
        IReadOnlyList<PlanObject> roots,
        PlanShape plan,
        Matching matching)
    {
        var candidates = roots.SelectMany((root, order) => plan.Tops.Select(top => (Root: root,
                                                                                     Order: order,
                                                                                     Operator: top,
                                                                                     Choice: matching.Fit(root, top))))
                              .Where(c => c.Choice.Score > 0)
                              .OrderByDescending(c => c.Choice.Score)
                              .ThenBy(c => c.Order)
                              .ThenBy(c => c.Operator.PlanNodeIdentifier!.PlanHandleId)
                              .ThenBy(c => c.Operator.PlanNodeIdentifier!.NodeId);

        var acceptedRoots = new HashSet<PlanObject>();

        var acceptedOperators = new HashSet<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance);

        foreach (var (root, _, operatorEvent, choice) in candidates)
        {
            if (!acceptedRoots.Contains(root) && acceptedOperators.Add(operatorEvent))
            {
                acceptedRoots.Add(root);

                yield return (root, operatorEvent, choice);
            }
        }
    }

    private static Dictionary<ulong, PlanNodeIdentifier> Instances(IReadOnlyList<LoggedCall> factories,
                                                                   IReadOnlyList<LoggedCall> constructors,
                                                                   IReadOnlyList<BatchIterator> batchIterators,
                                                                   Dictionary<ulong, PlanObject> objects,
                                                                   Dictionary<ulong, PlanNodeIdentifier> planObjects)
    {
        var instances = new Dictionary<ulong, PlanNodeIdentifier>();

        foreach (var constructor in constructors)
        {
            if (planObjects.TryGetValue(constructor.PlanObjectId, out var node))
            {
                instances.TryAdd(constructor.Object, node);
            }
        }

        foreach (var iterator in batchIterators)
        {
            if (planObjects.TryGetValue(iterator.PlanObjectId, out var node))
            {
                instances.TryAdd(iterator.Instance, node);
            }
        }

        var returned = factories.Where(f => IsRowFactory(f.Node)
                                            && f.Call is { Returned: true, ReturnValue: not 0 }
                                            && planObjects.ContainsKey(f.Object))
                                .GroupBy(f => (f.Thread, f.Call.ReturnValue))
                                .Select(g => g.MaxBy(f => objects[f.Object].Depth)!);

        foreach (var factory in returned)
        {
            instances.TryAdd(factory.Call.ReturnValue, planObjects[factory.Object]);
        }

        return instances;
    }

    private enum ChoiceType
    {
        Direct,
        SkipPlanNode,
        SkipPlanObject
    }

    private sealed record Choice(int Score,
                                 ChoiceType Type,
                                 IReadOnlyList<(PlanObject PlanObject, ExecutionOperatorEvent Operator)> Pairs);

    private sealed record BatchIterator(ulong Instance, ulong BatchObjectId, ulong PlanObjectId);

    private sealed class LoggedCall(CallStackNode node, int index, TraceArgumentCall call, int objectRegister)
    {
        public CallStackNode Node { get; } = node;

        public int Index { get; } = index;

        public TraceArgumentCall Call { get; } = call;

        public uint Thread => Call.ThreadId;

        public TracePosition FromPosition => Call.FromPosition;

        public ulong Object => Call.Slot(ObjectRegister);

        public ulong PlanObjectId => Call.Slot(PlanObjectRegister);

        private int ObjectRegister { get; } = objectRegister;

        public static int Compare(LoggedCall x, LoggedCall y)
        {
            var start = x.FromPosition.CompareTo(y.FromPosition);

            if (start != 0)
            {
                return start;
            }

            var order = x.Node.Order.CompareTo(y.Node.Order);

            return order != 0 ? order : x.Index.CompareTo(y.Index);
        }
    }

    private sealed class PlanObject(ulong address, LoggedCall first, IReadOnlyList<GlobPattern> objectOperators)
    {
        public static IComparer<PlanObject> ByFirstCall { get; }
            = Comparer<PlanObject>.Create((x, y) => LoggedCall.Compare(x.First, y.First));

        public ulong Address { get; } = address;

        public IReadOnlyList<GlobPattern> ObjectOperators { get; } = objectOperators;

        public List<PlanObject> Children { get; } = [];

        public PlanObject? Parent { get; private set; }

        public int Depth => Parent is null ? 0 : Parent.Depth + 1;

        private LoggedCall? Linked { get; set; }

        private LoggedCall First { get; } = first;

        public static Dictionary<ulong, PlanObject> Build(IReadOnlyList<LoggedCall> factories,
                                                          ILookup<CallStackNode, LoggedCall> factoryNodes,
                                                          Dictionary<ulong, List<GlobPattern>> operators)
        {
            var objects = new Dictionary<ulong, PlanObject>();

            var ordered = factories.GroupBy(f => f.Thread)
                                   .OrderByDescending(g => g.Count())
                                   .ThenBy(g => g.Key)
                                   .SelectMany(g => g.Order(Comparer<LoggedCall>.Create(LoggedCall.Compare)));

            foreach (var factory in ordered)
            {
                var planObject = GetObject(factory, objects, operators);

                if (planObject.Parent is not null
                    || GetParent(factory, factoryNodes) is not { } parentCall
                    || parentCall.Object == factory.Object)
                {
                    continue;
                }

                var parent = GetObject(parentCall, objects, operators);

                if (parent.IsWithin(planObject))
                {
                    continue;
                }

                planObject.Parent = parent;

                planObject.Linked = factory;

                parent.Children.Add(planObject);
            }

            foreach (var planObject in objects.Values)
            {
                planObject.Children.Sort((x, y) => LoggedCall.Compare(x.Linked!, y.Linked!));
            }

            return objects;
        }

        private static PlanObject GetObject(LoggedCall factory,
                                           Dictionary<ulong, PlanObject> objects,
                                           Dictionary<ulong, List<GlobPattern>> operators)
        {
            if (!objects.TryGetValue(factory.Object, out var planObject))
            {
                planObject = new PlanObject(factory.Object, factory, operators.GetValueOrDefault(factory.Object) ?? []);

                objects[factory.Object] = planObject;
            }

            return planObject;
        }

        private bool IsWithin(PlanObject ancestor)
        {
            for (var planObject = this; planObject is not null; planObject = planObject.Parent)
            {
                if (ReferenceEquals(planObject, ancestor))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class PlanShape(OperatorHierarchy hierarchy)
    {
        public IReadOnlyList<ExecutionOperatorEvent> Tops { get; } = [.. hierarchy.Operators
                                                                                  .Where(o => !IsStatement(o) && IsTop(hierarchy, o))
                                                                                  .OrderBy(o => o.PlanNodeIdentifier!.PlanHandleId)
                                                                                  .ThenBy(o => o.PlanNodeIdentifier!.NodeId)];

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

        private static bool IsStatement(ExecutionOperatorEvent operatorEvent) => operatorEvent.PlanNodeIdentifier!.NodeId < 0;

        private static bool IsTop(OperatorHierarchy hierarchy, ExecutionOperatorEvent operatorEvent)
            => hierarchy.Parent(operatorEvent) is null or { PlanNodeIdentifier.NodeId: < 0 };
    }

    private sealed class Matching(PlanShape plan)
    {
        private Dictionary<(PlanObject, PlanNodeIdentifier), Choice> Choices { get; } = new();

        public Choice Fit(PlanObject planObject, ExecutionOperatorEvent operatorEvent)
        {
            if (Choices.TryGetValue((planObject, operatorEvent.PlanNodeIdentifier!), out var choice))
            {
                return choice;
            }

            choice = Direct(planObject, operatorEvent);

            if (plan.Children(operatorEvent) is [var onlyNode]
                && Fit(planObject, onlyNode) is var skipPlanNode
                && skipPlanNode.Score - SkippedPlanNode > choice.Score)
            {
                choice = new Choice(skipPlanNode.Score - SkippedPlanNode, ChoiceType.SkipPlanNode, [(planObject, onlyNode)]);
            }

            if (planObject.ObjectOperators.Count == 0
                && planObject.Children is [var onlyObject]
                && Fit(onlyObject, operatorEvent) is var skipPlanObject
                && skipPlanObject.Score - SkippedPlanObject > choice.Score)
            {
                choice = new Choice(skipPlanObject.Score - SkippedPlanObject, ChoiceType.SkipPlanObject, [(onlyObject, operatorEvent)]);
            }

            Choices[(planObject, operatorEvent.PlanNodeIdentifier!)] = choice;

            return choice;
        }

        public void Assign(PlanObject planObject,
                           ExecutionOperatorEvent operatorEvent,
                           Choice choice,
                           Dictionary<ulong, PlanNodeIdentifier> planObjects)
        {
            if (choice.Type == ChoiceType.Direct)
            {
                planObjects.TryAdd(planObject.Address, operatorEvent.PlanNodeIdentifier!);
            }

            foreach (var (childObject, childOperator) in choice.Pairs)
            {
                Assign(childObject, childOperator, Fit(childObject, childOperator), planObjects);
            }
        }

        private Choice Direct(PlanObject planObject, ExecutionOperatorEvent operatorEvent)
        {
            var (score, pairs) = Align(planObject.Children, plan.Children(operatorEvent));

            return new Choice(score + NameScore(planObject, operatorEvent), ChoiceType.Direct, pairs);
        }

        private (int Score, List<(PlanObject, ExecutionOperatorEvent)> Pairs) Align(IReadOnlyList<PlanObject> planObjects,
                                                                                      IReadOnlyList<ExecutionOperatorEvent> operators)
        {
            var scores = new int[planObjects.Count + 1, operators.Count + 1];

            for (var i = 1; i <= planObjects.Count; i++)
            {
                scores[i, 0] = scores[i - 1, 0] - SkippedPlanObject;
            }

            for (var j = 1; j <= operators.Count; j++)
            {
                scores[0, j] = scores[0, j - 1] - SkippedPlanNode;
            }

            for (var i = 1; i <= planObjects.Count; i++)
            {
                for (var j = 1; j <= operators.Count; j++)
                {
                    var best = Math.Max(scores[i - 1, j] - SkippedPlanObject, scores[i, j - 1] - SkippedPlanNode);

                    if (Fit(planObjects[i - 1], operators[j - 1]) is { Score: > 0 } fit)
                    {
                        best = Math.Max(best, scores[i - 1, j - 1] + fit.Score);
                    }

                    scores[i, j] = best;
                }
            }

            var pairs = new List<(PlanObject, ExecutionOperatorEvent)>();

            var row = planObjects.Count;

            var column = operators.Count;

            while (row > 0 && column > 0)
            {
                if (Fit(planObjects[row - 1], operators[column - 1]) is { Score: > 0 } fit
                    && scores[row, column] == scores[row - 1, column - 1] + fit.Score)
                {
                    pairs.Add((planObjects[row - 1], operators[column - 1]));

                    row--;

                    column--;
                }
                else if (scores[row, column] == scores[row - 1, column] - SkippedPlanObject)
                {
                    row--;
                }
                else
                {
                    column--;
                }
            }

            pairs.Reverse();

            return (scores[planObjects.Count, operators.Count], pairs);
        }

        private static int NameScore(PlanObject planObject, ExecutionOperatorEvent operatorEvent)
        {
            if (planObject.ObjectOperators.Count == 0)
            {
                return 0;
            }

            return planObject.ObjectOperators.Any(o => o.Matches(operatorEvent.Name)) ? NameMatch : NameMismatch;
        }
    }
}
