using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests;

public class CallStackTreeTests(ITestOutputHelper output)
{
    [Fact]
    public void Truncated_Stack_Merges_Onto_The_Fuller_Path()
    {
        var tree = new CallStackTree();

        // Frames are innermost-first: event site Y, then X, ThreadEntryPoint, and (outermost) BaseThreadInitThunk.
        tree.Add([Frame("Y", 10), Frame("X", 20), Frame("ThreadEntryPoint", 30), Frame("BaseThreadInitThunk", 40)], Event());

        // The same path but truncated at the outer end (BaseThreadInitThunk dropped, as a deep capture would be).
        tree.Add([Frame("Y", 10), Frame("X", 20), Frame("ThreadEntryPoint", 30)], Event());

        var collapsed = tree.CollapseToFunctions();

        var text = collapsed.Render();

        output.WriteLine(text);

        // The truncated stack grafts on: ThreadEntryPoint appears once, under BaseThreadInitThunk, both events reach Y.
        Assert.Equal(
            """
            BaseThreadInitThunk::m
              ThreadEntryPoint::m
                X::m
                  Y::m [2]

            """.ReplaceLineEndings("\n"),
            text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_Recursive_Truncated_Stack_Does_Not_Create_A_Cycle()
    {
        var tree = new CallStackTree();

        // Innermost-first: site Y, then A, B, A — the OUTERMOST frame (A) recurs deeper in the same stack. Truncated
        // (no thread-entry frames above), so on collapse A is a root child that also appears inside its own subtree.
        // Grafting the root child onto that inner copy would make it its own ancestor — a cycle. The graft must be
        // skipped, and the collapse must terminate with an acyclic tree.
        tree.Add([Frame("Y", 10), Frame("A", 20), Frame("B", 30), Frame("A", 40)], Event());

        var collapsed = tree.CollapseToFunctions();

        // Every node's parent chain reaches the root without revisiting — i.e. no cycle (this walk would hang otherwise).
        foreach (var node in collapsed.Nodes())
        {
            var seen = new HashSet<CallStackNode>();

            for (var current = node; current is not null; current = current.Parent)
            {
                Assert.True(seen.Add(current), "call-stack tree parent chain contains a cycle");
            }
        }
    }

    private static int _sequence;

    private static EngineEvent Event() => new() { Name = "e", SequenceId = _sequence++ };

    private static CallstackFrame Frame(string name, uint rva) => new()
    {
        Module = "sqllang",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = name, MethodName = "m" },
    };
}
