using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events.EventTypes;
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

    private static int _sequence;

    private static EngineEvent Event() => new() { Name = "e", SequenceId = _sequence++ };

    private static CallstackFrame Frame(string name, uint rva) => new()
    {
        Module = "sqllang",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = name, MethodName = "m" },
    };
}
