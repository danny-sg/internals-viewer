using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

internal static class AllocationMapper
{
    private const int MaximumObjectDepth = 16;

    private const string UnknownClerk = "Unknown Clerk";

    private const string WorkspaceClerk = "MEMORYCLERK_SQLQERESERVATIONS";

    public static (MemoryIndex Memory, long Allocations, ulong Bytes) Map(TraceCallStore store,
                                                                          IReadOnlyList<MemoryFunction> functions,
                                                                          TraceTimeline? timeline,
                                                                          MemoryClerkSnapshot? clerks = null)
    {
        var byAddress = functions.ToDictionary(f => f.Address);

        var lists = new Dictionary<ulong, TraceCallList?>();

        var scanned = timeline is null ? new ScannedCalls() : ScanSpans(GetCalls, byAddress, timeline, clerks ?? MemoryClerkSnapshot.Empty);

        var plumbing = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        var nested = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        var values = new Dictionary<ulong, CallValues>();

        long count = 0;

        ulong bytes = 0;

        foreach (var function in functions.Where(f => f.IsAllocation || f is { Operation: AllocationOperation.Free, PointerSlot: >= 0 }))
        {
            if (GetCalls(function.Address) is not { } logged)
            {
                continue;
            }

            var callValues = new CallValues(function, logged.Count);

            for (var index = 0; index < logged.Count; index++)
            {
                var call = logged[index];

                var node = store.GetNode(call);

                if (!function.IsCounted(call) || (function.IsPage && IsPlumbing(node, plumbing)))
                {
                    continue;
                }

                var pointer = function.GetPointer(call);

                if (!function.IsAllocation)
                {
                    callValues.Released[index] = pointer;

                    continue;
                }

                callValues.Bytes[index] = function.GetByteCount(call);

                callValues.Returned[index] = call.ReturnedValue;

                if (function.Operation == AllocationOperation.Reallocate && pointer != 0)
                {
                    callValues.Released[index] = pointer;
                }

                if (node is null || IsNested(node, byAddress, nested))
                {
                    continue;
                }

                node.AllocatedBytes += (long)callValues.Bytes[index];

                node.Allocations++;

                bytes += callValues.Bytes[index];

                count++;
            }

            values[function.Address] = callValues;
        }

        var (allocations, frees) = scanned.MemoryOf(values);

        return (MemoryIndex.Build(allocations, frees), count, bytes);

        TraceCallList? GetCalls(ulong address)
            => lists.TryGetValue(address, out var list) ? list : lists[address] = store.GetCallList(address, 0);
    }

    private static ScannedCalls ScanSpans(Func<ulong, TraceCallList?> getCalls,
                                          Dictionary<ulong, MemoryFunction> functions,
                                          TraceTimeline timeline,
                                          MemoryClerkSnapshot snapshot)
    {
        var scanned = new ScannedCalls();

        var threads = new Dictionary<uint, ThreadCalls>();

        foreach (var span in timeline.GetSpanLocations())
        {
            if (span.Node.Frame is not { } frame
                || !functions.TryGetValue(frame.Address, out var function)
                || span.Call is not (>= 0 and var call)
                || getCalls(function.Address) is not { } logged
                || call >= logged.Count)
            {
                continue;
            }

            var start = span.StartOf(TraceTimelineAxis.Position);

            var end = span.EndOf(TraceTimelineAxis.Position);

            if (!threads.TryGetValue(span.Thread.ThreadId, out var thread))
            {
                thread = new ThreadCalls(span.Thread.ThreadId);

                threads[thread.ThreadId] = thread;

                scanned.Threads.Add(thread);
            }

            thread.Calls.Add((function.Address, call, start, end));

            if (function is { CarriesClerk: false, IsObjectCall: false })
            {
                continue;
            }

            var arguments = logged[call];

            if (function.CarriesClerk)
            {
                thread.Pages.Add((start, arguments.Slot(0)));
            }

            if (function.IsObjectCall && function.GetObject(arguments) is not 0 and var memoryObject)
            {
                thread.Objects.Add((start, end, memoryObject));
            }
        }

        var objectClerks = new Dictionary<ulong, ulong>();

        var objectParents = new Dictionary<ulong, ulong>();

        foreach (var thread in scanned.Threads)
        {
            thread.Sort();

            foreach (var (start, end, memoryObject) in thread.Objects)
            {
                if (FirstWithin(thread.PageStarts, start, end) is >= 0 and var page)
                {
                    objectClerks.TryAdd(memoryObject, thread.Pages[page].Clerk);
                }

                if (FirstWithin(thread.ObjectStarts, Math.BitIncrement(start), end) is >= 0 and var nested
                    && thread.Objects[nested].Object != memoryObject)
                {
                    objectParents.TryAdd(memoryObject, thread.Objects[nested].Object);
                }
            }
        }

        foreach (var thread in scanned.Threads)
        {
            foreach (var (address, call, start, end) in thread.Calls)
            {
                if (!functions[address].IsAllocation)
                {
                    continue;
                }

                var kind = FirstWithin(thread.PageStarts, start, end) is >= 0 and var page
                    ? ClerkType(snapshot, thread.Pages[page].Clerk)
                    : null;

                if (kind is null && FirstWithin(thread.ObjectStarts, start, end) is >= 0 and var inner)
                {
                    kind = ObjectType(thread.Objects[inner].Object, objectClerks, objectParents, snapshot);
                }

                if (kind is null)
                {
                    continue;
                }

                if (!scanned.Kinds.TryGetValue(address, out var kinds))
                {
                    kinds = new string?[getCalls(address)!.Count];

                    scanned.Kinds[address] = kinds;
                }

                kinds[call] = kind;
            }
        }

        return scanned;
    }

    private static string ClerkType(MemoryClerkSnapshot snapshot, ulong clerk)
        => snapshot.Clerks.GetValueOrDefault(clerk) ?? $"{UnknownClerk} 0x{clerk:X}";

    private static string? ObjectType(ulong memoryObject,
                                      Dictionary<ulong, ulong> objectClerks,
                                      Dictionary<ulong, ulong> objectParents,
                                      MemoryClerkSnapshot snapshot)
    {
        for (var depth = 0; depth < MaximumObjectDepth && memoryObject != 0; depth++)
        {
            if (objectClerks.TryGetValue(memoryObject, out var clerk))
            {
                return ClerkType(snapshot, clerk);
            }

            if (snapshot.Objects.TryGetValue(memoryObject, out var type))
            {
                return type;
            }

            memoryObject = objectParents.GetValueOrDefault(memoryObject);
        }

        return null;
    }

    private static int FirstWithin(double[] starts, double start, double end)
    {
        var index = SortedSearch.FirstAtOrAfter(starts, start);

        return index < starts.Length && starts[index] < end ? index : -1;
    }

    private static bool IsPlumbing(CallStackNode? node, Dictionary<CallStackNode, bool> plumbing)
    {
        if (node is null)
        {
            return false;
        }

        if (!plumbing.TryGetValue(node, out var inside))
        {
            inside = node.Ancestors().Skip(1).Any(a => MemoryFunction.IsMemoryObjectClass(a.Frame?.Resolved?.ClassName));

            plumbing[node] = inside;
        }

        return inside;
    }

    private static bool IsNested(CallStackNode node, Dictionary<ulong, MemoryFunction> functions, Dictionary<CallStackNode, bool> nested)
    {
        if (!nested.TryGetValue(node, out var inside))
        {
            inside = node.Ancestors()
                         .Skip(1)
                         .Any(a => a.Frame is { } frame && functions.TryGetValue(frame.Address, out var function) && function.IsAllocation);

            nested[node] = inside;
        }

        return inside;
    }

    private sealed class CallValues(MemoryFunction function, int count)
    {
        public ulong[] Bytes { get; } = function.IsAllocation ? new ulong[count] : [];

        public ulong[] Returned { get; } = function.IsAllocation ? new ulong[count] : [];

        public ulong[] Released { get; } = function.Operation is AllocationOperation.Free or AllocationOperation.Reallocate
            ? new ulong[count]
            : [];
    }

    private sealed class ScannedCalls
    {
        public Dictionary<ulong, string?[]> Kinds { get; } = [];

        public List<ThreadCalls> Threads { get; } = [];

        public (List<MemoryAllocation> Allocations, List<MemoryFree> Frees) MemoryOf(Dictionary<ulong, CallValues> values)
        {
            var allocations = new List<MemoryAllocation>();

            var frees = new List<MemoryFree>();

            foreach (var thread in Threads)
            {
                foreach (var (address, call, start, end) in thread.Calls)
                {
                    if (!values.TryGetValue(address, out var callValues))
                    {
                        continue;
                    }

                    if (call < callValues.Bytes.Length && callValues.Bytes[call] > 0)
                    {
                        var workspace = Kinds.TryGetValue(address, out var kinds) && kinds[call] == WorkspaceClerk;

                        allocations.Add(new MemoryAllocation(thread.ThreadId,
                                                             start,
                                                             end,
                                                             callValues.Bytes[call],
                                                             callValues.Returned[call],
                                                             workspace));
                    }

                    if (call < callValues.Released.Length && callValues.Released[call] != 0)
                    {
                        frees.Add(new MemoryFree(thread.ThreadId, start, end, callValues.Released[call]));
                    }
                }
            }

            return (allocations, frees);
        }
    }

    private sealed class ThreadCalls(uint threadId)
    {
        public uint ThreadId { get; } = threadId;

        public List<(ulong Address, int Call, double Start, double End)> Calls { get; } = [];

        public List<(double Start, ulong Clerk)> Pages { get; } = [];

        public List<(double Start, double End, ulong Object)> Objects { get; } = [];

        public double[] PageStarts { get; private set; } = [];

        public double[] ObjectStarts { get; private set; } = [];

        public void Sort()
        {
            Pages.Sort();

            Objects.Sort();

            PageStarts = [.. Pages.Select(p => p.Start)];

            ObjectStarts = [.. Objects.Select(o => o.Start)];
        }
    }
}
