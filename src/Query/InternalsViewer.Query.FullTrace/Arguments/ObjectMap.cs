using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed class ObjectMap
{
    private const ulong SmallestAddress = 0x10000;

    private ObjectMap(ObjectFunctions functions,
                      Dictionary<ulong, TraceObject[]> objects,
                      AllocationMap allocations,
                      TraceTimeline? timeline)
    {
        Functions = functions;
        Objects = objects;
        Allocations = allocations;
        Timeline = timeline;
    }

    public static ObjectMap Empty { get; } = new(ObjectFunctions.Empty, [], AllocationMap.Empty, null);

    public int Count => Objects.Values.Sum(o => o.Length);

    public AllocationMap Allocations { get; }

    internal ObjectFunctions Functions { get; }

    private Dictionary<ulong, TraceObject[]> Objects { get; }

    private TraceTimeline? Timeline { get; }

    public static ObjectMap Resolve(TraceResult trace, string symbolsPath)
        => Build(trace.CallLog, trace.Timeline, ObjectFunctionResolver.Resolve(trace.Timeline.MappedNodes, symbolsPath));

    public TraceObject? GetObject(ulong address, TraceArgumentCall call)
        => Timeline is not null && Objects.ContainsKey(address) ? GetObject(address, Timeline.GetPosition(call)) : null;

    public TraceAllocation? GetAllocation(ulong address, TraceArgumentCall call)
        => Timeline is not null && Allocations.Contains(address) ? Allocations.GetAllocation(address, Timeline.GetPosition(call)) : null;

    public TraceObject? GetObject(ulong address, double position)
    {
        if (!Objects.TryGetValue(address, out var objects))
        {
            return null;
        }

        foreach (var candidate in objects)
        {
            if (candidate.First <= position && position <= candidate.Last)
            {
                return candidate;
            }
        }

        return null;
    }

    internal static ObjectMap Build(TraceCallLog callLog, TraceTimeline timeline, ObjectFunctions functions)
    {
        var members = GetMemberCalls(callLog, functions.Members);

        var nodes = new Dictionary<int, MemberCalls>();

        foreach (var member in members)
        {
            foreach (var node in member.Nodes)
            {
                nodes[node] = member;
            }
        }

        var sightings = new Dictionary<ulong, List<Sighting>>();

        foreach (var thread in timeline.Threads)
        {
            foreach (var row in thread.Rows)
            {
                var starts = row.Starts(TraceTimelineAxis.Position);

                var ends = row.Ends(TraceTimelineAxis.Position);

                for (var index = 0; index < row.Count; index++)
                {
                    if (!nodes.TryGetValue(row.NodeAt(index), out var member)
                        || row.CallAt(index) is not (>= 0 and var call)
                        || call >= member.Calls.Length)
                    {
                        continue;
                    }

                    member.Positioned[call] = true;

                    Add(sightings, member.Calls[call].Self, new Sighting(starts[index], ends[index], member.Member));
                }
            }
        }

        foreach (var member in members)
        {
            for (var call = 0; call < member.Calls.Length; call++)
            {
                if (!member.Positioned[call])
                {
                    var (sequence, _, self) = member.Calls[call];

                    Add(sightings, self, new Sighting(sequence, sequence, member.Member));
                }
            }
        }

        var objects = sightings.ToDictionary(s => s.Key, s => Group(s.Key, s.Value).ToArray());

        var allocations = AllocationMap.Build(callLog, timeline, functions);

        foreach (var call in allocations.Calls)
        {
            if (objects.TryGetValue(call.Pointer, out var candidates))
            {
                Link(candidates, call);
            }
        }

        return new ObjectMap(functions, objects, allocations, timeline);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Functions.Members.Count);

        foreach (var (address, member) in Functions.Members)
        {
            writer.Write(address);

            writer.Write(member.ClassName);

            writer.Write((byte)member.Kind);
        }

        writer.Write(Functions.Allocators.Count);

        foreach (var address in Functions.Allocators)
        {
            writer.Write(address);
        }

        writer.Write(Functions.Frees.Count);

        foreach (var (address, slot) in Functions.Frees)
        {
            writer.Write(address);

            writer.Write(slot);
        }
    }

    internal static ObjectMap Read(BinaryReader reader, TraceCallLog callLog, TraceTimeline timeline)
    {
        var count = reader.ReadInt32();

        var members = new Dictionary<ulong, TraceMember>(count);

        for (var index = 0; index < count; index++)
        {
            members[reader.ReadUInt64()] = new TraceMember(reader.ReadString(), (TraceMemberKind)reader.ReadByte());
        }

        var allocators = new HashSet<ulong>();

        for (var index = reader.ReadInt32(); index > 0; index--)
        {
            allocators.Add(reader.ReadUInt64());
        }

        var frees = new Dictionary<ulong, int>();

        for (var index = reader.ReadInt32(); index > 0; index--)
        {
            frees[reader.ReadUInt64()] = reader.ReadInt32();
        }

        return Build(callLog, timeline, new ObjectFunctions(members, allocators, frees));
    }

    private static List<MemberCalls> GetMemberCalls(TraceCallLog callLog, IReadOnlyDictionary<ulong, TraceMember> members)
    {
        var functions = new List<MemberCalls>();

        foreach (var (address, instance) in callLog.FunctionKeys)
        {
            if (members.TryGetValue(address, out var member))
            {
                functions.Add(new MemberCalls(member, [.. callLog.GetSelves(address, instance)]));
            }
        }

        return functions;
    }

    private static void Add(Dictionary<ulong, List<Sighting>> sightings, ulong self, Sighting sighting)
    {
        if (self < SmallestAddress)
        {
            return;
        }

        if (!sightings.TryGetValue(self, out var list))
        {
            list = [];

            sightings[self] = list;
        }

        list.Add(sighting);
    }

    private static void Link(TraceObject[] candidates, AllocationCall allocation)
    {
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];

            if (candidate.First < allocation.Start)
            {
                continue;
            }

            if (candidate.First <= allocation.ConstructedBy && (index == 0 || candidates[index - 1].Last < allocation.Start))
            {
                candidates[index] = candidate with { First = allocation.Start };
            }

            return;
        }
    }

    private static IEnumerable<TraceObject> Group(ulong address, List<Sighting> sightings)
    {
        sightings.Sort((left, right) => left.Start.CompareTo(right.Start));

        var scope = new Scope();

        foreach (var sighting in sightings)
        {
            if (scope.Ends(sighting))
            {
                foreach (var traceObject in scope.GetObjects(address))
                {
                    yield return traceObject;
                }

                scope = new Scope();
            }

            scope.Add(sighting);
        }

        foreach (var traceObject in scope.GetObjects(address))
        {
            yield return traceObject;
        }
    }

    private readonly record struct Sighting(double Start, double End, TraceMember Member);

    private sealed class MemberCalls(TraceMember member, (ulong Sequence, int Node, ulong Self)[] calls)
    {
        public TraceMember Member { get; } = member;

        public (ulong Sequence, int Node, ulong Self)[] Calls { get; } = calls;

        public bool[] Positioned { get; } = new bool[calls.Length];

        public IEnumerable<int> Nodes => Calls.Select(c => c.Node).Distinct();
    }

    private sealed class Scope
    {
        private List<Sighting> Sightings { get; } = [];

        private double? Constructed { get; set; }

        private double? Destroyed { get; set; }

        private bool InUse { get; set; }

        public bool Ends(Sighting sighting) => Sightings.Count > 0 && sighting.Member.Kind switch
        {
            TraceMemberKind.Constructor => InUse || Destroyed is not null,
            TraceMemberKind.Destructor => false,
            _ => Destroyed is { } destroyed && sighting.Start > destroyed
        };

        public void Add(Sighting sighting)
        {
            Sightings.Add(sighting);

            switch (sighting.Member.Kind)
            {
                case TraceMemberKind.Constructor:
                {
                    Constructed = Math.Max(Constructed ?? sighting.End, sighting.End);

                    break;
                }
                case TraceMemberKind.Destructor:
                {
                    Destroyed = Math.Max(Destroyed ?? sighting.End, sighting.End);

                    break;
                }
                default:
                {
                    InUse |= Constructed is not { } constructed || sighting.Start > constructed;

                    break;
                }
            }
        }

        public IEnumerable<TraceObject> GetObjects(ulong address)
        {
            var classes = Sightings.GroupBy(s => s.Member.ClassName, StringComparer.Ordinal)
                                   .Select(g => (ClassName: g.Key, First: g.Min(s => s.Start), Last: g.Max(s => s.End), Calls: g.Count()))
                                   .OrderBy(c => c.First)
                                   .ToList();

            if (Constructed is not null)
            {
                yield return GetObject(address, classes);

                yield break;
            }

            var cluster = new List<(string ClassName, double First, double Last, int Calls)>();

            var last = double.MinValue;

            foreach (var current in classes)
            {
                if (cluster.Count > 0 && current.First > last)
                {
                    yield return GetObject(address, cluster);

                    cluster.Clear();
                }

                cluster.Add(current);

                last = Math.Max(last, current.Last);
            }

            if (cluster.Count > 0)
            {
                yield return GetObject(address, cluster);
            }
        }

        private static TraceObject GetObject(ulong address, List<(string ClassName, double First, double Last, int Calls)> cluster)
            => new(address, cluster.MaxBy(c => c.Calls).ClassName, cluster.Min(c => c.First), cluster.Max(c => c.Last));
    }
}
