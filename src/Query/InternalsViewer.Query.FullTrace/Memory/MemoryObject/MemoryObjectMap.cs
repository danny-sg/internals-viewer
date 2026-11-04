using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace.Memory.MemoryObject;

/// <summary>
/// SQL Server Memory Object (PMO) page ownership map
/// </summary>
/// <remarks>
/// SQL Server Memory Objects (PMOs) hand out sub-allocations from 8 KB pages (or larger, given by the object's page size) obtained via a
/// memory clerk.
///
/// An address inside a page belongs to the memory object that claimed the page, which attributes raw addresses to the plan, statement or
/// cache object that owns them.
///
/// The map is built from the traced calls to the memory object functions (Alloc, Realloc, Free, PvMark, FreeToMark). Each call carries the
/// memory object as its instance argument and a page pointer in either an argument slot or the return value, defined by PointerSlots. The
/// pointer is rounded down to its page, and a Claim (position, thread, memory object) is recorded against the page.
///
/// A page can be claimed by different objects over the trace as pages are freed and reused, so claims are kept in position order and
/// GetOwner answers for a specific position - the owner is the latest claim at or before that position.
///
/// Object identity and type come from sys.dm_os_memory_objects (MemoryObjectInfo), captured alongside the trace.
/// </remarks>
public sealed partial class MemoryObjectMap
{
    private const int ReturnSlot = -1;

    private const int ObjectSlot = 0;

    private static readonly Dictionary<string, int[]> PointerSlots = new(StringComparer.Ordinal)
    {
        ["Alloc"] = [ReturnSlot],
        ["Realloc"] = [ReturnSlot, 1],
        ["Free"] = [1],
        ["PvMark"] = [ReturnSlot],
        ["FreeToMark"] = [1]
    };

    private MemoryObjectMap(Dictionary<(ulong Page, int Size), Claim[]> pages, Dictionary<ulong, MemoryObjectInfo> objects)
    {
        Pages = pages;
        Objects = objects;
        PageSizes = [.. pages.Keys.Select(p => p.Size).Distinct().Order()];
    }

    public static MemoryObjectMap Empty { get; } = new([], []);

    private Dictionary<(ulong Page, int Size), Claim[]> Pages { get; }

    private Dictionary<ulong, MemoryObjectInfo> Objects { get; }

    private int[] PageSizes { get; }

    public static MemoryObjectMap Build(TraceCallStore store,
                                        IReadOnlyList<CallStackNode?> nodes,
                                        IReadOnlyDictionary<ulong, MemoryObjectInfo> objects)
    {
        var claims = new Dictionary<(ulong Page, int Size), List<Claim>>();

        foreach (var (address, instance, slots) in GetFunctions(nodes))
        {
            if (store.GetCallList(address, instance) is not { } calls)
            {
                continue;
            }

            foreach (var call in calls)
            {
                var memoryObject = call.Slot(ObjectSlot);

                if (memoryObject == 0)
                {
                    continue;
                }

                var size = objects.TryGetValue(memoryObject, out var info) && info.PageSize > 0 ? info.PageSize : PageData.Size;

                foreach (var slot in slots)
                {
                    var pointer = slot == ReturnSlot ? call.ReturnedValue : call.Slot(slot);

                    if (pointer < TraceAddress.MinAddress)
                    {
                        continue;
                    }

                    var key = (GetPage(pointer, size), size);

                    if (!claims.TryGetValue(key, out var page))
                    {
                        page = [];

                        claims[key] = page;
                    }

                    page.Add(new Claim(call.FromPosition, call.ThreadId, memoryObject));
                }
            }
        }

        var owners = claims.Values.SelectMany(c => c).Select(c => c.MemoryObject).ToHashSet();

        return new MemoryObjectMap(claims.ToDictionary(c => c.Key, c => c.Value.OrderBy(v => v.Position).ToArray()),
                                   objects.Where(o => owners.Contains(o.Key)).ToDictionary());
    }

    public MemoryObjectReference? GetOwner(ulong address, TracePosition position)
    {
        if (address < TraceAddress.MinAddress)
        {
            return null;
        }

        foreach (var size in PageSizes)
        {
            if (!Pages.TryGetValue((GetPage(address, size), size), out var claims))
            {
                continue;
            }

            var index = position.IsKnown ? SortedSearch.FirstAfter(claims, position, static c => c.Position) - 1 : 0;

            var owner = claims[Math.Max(index, 0)].MemoryObject;

            return new MemoryObjectReference(owner, Objects.GetValueOrDefault(owner)?.Type, size);
        }

        return null;
    }

    internal IEnumerable<MemoryBlock> GetPageBlocks()
        => Pages.Select(p => new MemoryBlock(p.Key.Page, (ulong)p.Key.Size, p.Value[0].Thread, p.Value[0].Position, TracePosition.Unknown));

    private static ulong GetPage(ulong address, int size) => address & ~((ulong)size - 1);

    private static IEnumerable<(ulong Address, ulong Instance, int[] Slots)> GetFunctions(IReadOnlyList<CallStackNode?> nodes)
        => nodes.OfType<CallStackNode>()
                .Select(n => n.Frame)
                .OfType<CallstackFrame>()
                .Where(f => f.Resolved is { } resolved
                            && AllocationFunction.IsMemoryObjectClass(resolved.ClassName)
                            && PointerSlots.ContainsKey(resolved.MethodName))
                .Select(f => (f.Address, f.InstanceAddress, PointerSlots[f.Resolved!.MethodName]))
                .Distinct();

    private readonly record struct Claim(TracePosition Position, uint Thread, ulong MemoryObject);
}
