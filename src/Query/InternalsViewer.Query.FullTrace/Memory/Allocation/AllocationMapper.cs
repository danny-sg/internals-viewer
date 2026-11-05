using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

/// <summary>
/// Maps Trace calls to memory allocations and memory objects
/// </summary>
/// <remarks>
/// Calls are identified against a list of memory functions (referenced via RVA) to provide the Memory Index and Memory Objects.
/// </remarks>
internal static class AllocationMapper
{
    private const int MaximumObjectDepth = 16;

    private const string UnknownClerk = "Unknown Clerk";

    private const string WorkspaceClerk = "MEMORYCLERK_SQLQERESERVATIONS";

    public static (AllocationIndex Memory, long Allocations, ulong Bytes, AllocationMemoryObjectLookup MemoryObjects) 
        Map(TraceCallStore store,
            IReadOnlyList<AllocationFunction> functions,
            TraceTimeline? timeline,
            MemoryClerkSnapshot? clerks = null)
    {
        var byAddress = functions.ToDictionary(f => f.Address);

        var lists = new Dictionary<ulong, TraceCallList?>();

        var scanned = timeline is null ? new ScannedCallList() : ScanSpans(GetCalls, byAddress, timeline, clerks ?? MemoryClerkSnapshot.Empty);

        var plumbing = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        var nested = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        var values = new Dictionary<ulong, CallValueBuffer>();

        long count = 0;

        ulong bytes = 0;

        foreach (var function in functions.Where(f => f.IsAllocation || f is { Operation: AllocationOperation.Free, PointerArgumentSlot: >= 0 }))
        {
            if (GetCalls(function.Address) is not { } logged)
            {
                continue;
            }

            var callValues = new CallValueBuffer(function, logged.Count);

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

        return (AllocationIndex.Build(allocations, frees), count, bytes, new AllocationMemoryObjectLookup(scanned.ObjectTypes));

        TraceCallList? GetCalls(ulong address)
            => lists.TryGetValue(address, out var list) ? list : lists[address] = store.GetCallList(address, 0);
    }

    private static ScannedCallList ScanSpans(Func<ulong, TraceCallList?> getCalls,
                                          Dictionary<ulong, AllocationFunction> functions,
                                          TraceTimeline timeline,
                                          MemoryClerkSnapshot snapshot)
    {
        var scanned = new ScannedCallList();

        var threads = new Dictionary<uint, ThreadCallList>();

        foreach (var span in timeline.GetSpanLocations())
        {
            if (span.Node.Frame is not { } frame
                || !functions.TryGetValue(frame.Address, out var function)
                || span.CallIndex is not (>= 0 and var callIndex)
                || getCalls(function.Address) is not { } logged
                || callIndex >= logged.Count)
            {
                continue;
            }

            var start = span.From.Position;

            var end = span.To.Position;

            if (!threads.TryGetValue(span.Thread.ThreadId, out var thread))
            {
                thread = new ThreadCallList(span.Thread.ThreadId);

                threads[thread.ThreadId] = thread;

                scanned.Threads.Add(thread);
            }

            thread.Calls.Add((function.Address, callIndex, start, end));

            if (function is { CarriesClerk: false, IsObjectCall: false })
            {
                continue;
            }

            var arguments = logged[callIndex];

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

                if (FirstWithin(thread.ObjectStarts, start.Next, end) is >= 0 and var nested
                    && thread.Objects[nested].Object != memoryObject)
                {
                    objectParents.TryAdd(memoryObject, thread.Objects[nested].Object);
                }
            }
        }

        foreach (var thread in scanned.Threads)
        {
            foreach (var (address, callIndex, start, end) in thread.Calls)
            {
                if (!functions[address].IsAllocation)
                {
                    continue;
                }

                var inner = FirstWithin(thread.ObjectStarts, start, end);

                var memoryObject = inner >= 0 ? thread.Objects[inner].Object : 0;

                if (GetObjectType(memoryObject, objectParents, snapshot) is { } objectType)
                {
                    SetValue(scanned.ObjectTypes, address, callIndex, objectType, getCalls);
                }

                var kind = FirstWithin(thread.PageStarts, start, end) is >= 0 and var page
                    ? GetClerkType(snapshot, thread.Pages[page].Clerk)
                    : GetObjectClerk(memoryObject, objectClerks, objectParents, snapshot);

                if (kind is not null)
                {
                    SetValue(scanned.Kinds, address, callIndex, kind, getCalls);
                }
            }
        }

        return scanned;
    }

    private static void SetValue(Dictionary<ulong, string?[]> values,
                                 ulong address,
                                 int callIndex,
                                 string value,
                                 Func<ulong, TraceCallList?> getCalls)
    {
        if (!values.TryGetValue(address, out var calls))
        {
            calls = new string?[getCalls(address)!.Count];

            values[address] = calls;
        }

        calls[callIndex] = value;
    }

    private static string GetClerkType(MemoryClerkSnapshot snapshot, ulong clerk)
        => snapshot.Clerks.GetValueOrDefault(clerk) ?? $"{UnknownClerk} 0x{clerk:X}";

    private static string? GetObjectClerk(ulong memoryObject,
                                          Dictionary<ulong, ulong> objectClerks,
                                          Dictionary<ulong, ulong> objectParents,
                                          MemoryClerkSnapshot snapshot)
    {
        for (var depth = 0; depth < MaximumObjectDepth && memoryObject != 0; depth++)
        {
            if (objectClerks.TryGetValue(memoryObject, out var clerk))
            {
                return GetClerkType(snapshot, clerk);
            }

            if (snapshot.ObjectClerks.TryGetValue(memoryObject, out var type))
            {
                return type;
            }

            memoryObject = objectParents.GetValueOrDefault(memoryObject);
        }

        return null;
    }

    private static string? GetObjectType(ulong memoryObject, Dictionary<ulong, ulong> objectParents, MemoryClerkSnapshot snapshot)
    {
        for (var depth = 0; depth < MaximumObjectDepth && memoryObject != 0; depth++)
        {
            if (snapshot.MemoryObjects.TryGetValue(memoryObject, out var info))
            {
                return info.Type;
            }

            memoryObject = objectParents.GetValueOrDefault(memoryObject);
        }

        return null;
    }

    private static int FirstWithin(TracePosition[] starts, TracePosition start, TracePosition end)
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
            inside = node.Ancestors().Skip(1).Any(a => AllocationFunction.IsMemoryObjectClass(a.Frame?.Function?.ClassName));

            plumbing[node] = inside;
        }

        return inside;
    }

    private static bool IsNested(CallStackNode node, Dictionary<ulong, AllocationFunction> functions, Dictionary<CallStackNode, bool> nested)
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

    private sealed class CallValueBuffer(AllocationFunction function, int count)
    {
        public ulong[] Bytes { get; } = function.IsAllocation ? new ulong[count] : [];

        public ulong[] Returned { get; } = function.IsAllocation ? new ulong[count] : [];

        public ulong[] Released { get; } = function.Operation is AllocationOperation.Free or AllocationOperation.Reallocate
            ? new ulong[count]
            : [];
    }

    private sealed class ScannedCallList
    {
        public Dictionary<ulong, string?[]> Kinds { get; } = [];

        public Dictionary<ulong, string?[]> ObjectTypes { get; } = [];

        public List<ThreadCallList> Threads { get; } = [];

        public (List<MemoryAllocation> Allocations, List<MemoryFree> Frees) MemoryOf(Dictionary<ulong, CallValueBuffer> values)
        {
            var allocations = new List<MemoryAllocation>();

            var frees = new List<MemoryFree>();

            foreach (var thread in Threads)
            {
                foreach (var (address, callIndex, start, end) in thread.Calls)
                {
                    if (!values.TryGetValue(address, out var callValues))
                    {
                        continue;
                    }

                    if (callIndex < callValues.Bytes.Length && callValues.Bytes[callIndex] > 0)
                    {
                        var workspace = Kinds.TryGetValue(address, out var kinds) && kinds[callIndex] == WorkspaceClerk;

                        allocations.Add(new MemoryAllocation(thread.ThreadId,
                                                             start,
                                                             end,
                                                             callValues.Bytes[callIndex],
                                                             callValues.Returned[callIndex],
                                                             workspace));
                    }

                    if (callIndex < callValues.Released.Length && callValues.Released[callIndex] != 0)
                    {
                        frees.Add(new MemoryFree(thread.ThreadId, start, end, callValues.Released[callIndex]));
                    }
                }
            }

            return (allocations, frees);
        }
    }

    private sealed class ThreadCallList(uint threadId)
    {
        public uint ThreadId { get; } = threadId;

        public List<(ulong Address, int CallIndex, TracePosition Start, TracePosition End)> Calls { get; } = [];

        public List<(TracePosition Start, ulong Clerk)> Pages { get; } = [];

        public List<(TracePosition Start, TracePosition End, ulong Object)> Objects { get; } = [];

        public TracePosition[] PageStarts { get; private set; } = [];

        public TracePosition[] ObjectStarts { get; private set; } = [];

        public void Sort()
        {
            Pages.Sort();

            Objects.Sort();

            PageStarts = [.. Pages.Select(p => p.Start)];

            ObjectStarts = [.. Objects.Select(o => o.Start)];
        }
    }
}
