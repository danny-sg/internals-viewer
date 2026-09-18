using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests;

public class ReadAheadClassifierTests
{
    [Fact]
    public void Classify_Marks_A_Read_Issued_From_The_Read_Ahead_Path()
    {
        var stack = Stack(("BPool", "GetFromDisk"), ("RowGroupReadAheadManager", "ReadAheadNextRowGroups"), ("RowGroupManager", "GetNextRowGroup"));

        var readAhead = new ReadEventGroup { Events = [new LatchEvent { CallStack = stack }] };

        var build = new ReadEventGroup
        {
            Events = [new LatchEvent { CallStack = Stack(("BPool", "Get"), ("ColumnStoreObjectManager", "DeserializeLobObjectFromDisk")) }],
        };

        var noStack = new ReadEventGroup { Events = [] };

        ReadAheadClassifier.Classify([readAhead, build, noStack]);

        Assert.True(readAhead.IsReadAhead);
        Assert.False(build.IsReadAhead);
        Assert.False(noStack.IsReadAhead);
    }

    [Fact]
    public void Classify_Marks_A_Buffer_Pool_Read_Ahead()
    {
        var read = new ReadEventGroup { Events = [], CallStack = Stack(("BPool", "GetFromDisk"), ("BPool", "ReadAhead")) };

        ReadAheadClassifier.Classify([read]);

        Assert.True(read.IsReadAhead);
    }

    private static CallStackNode Stack(params (string ClassName, string MethodName)[] frames)
    {
        CallStackNode? parent = null;

        foreach (var (className, methodName) in frames.Reverse())
        {
            parent = new CallStackNode
            {
                Parent = parent,
                Frame = new CallstackFrame { Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = methodName } },
            };
        }

        return parent!;
    }
}
