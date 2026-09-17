using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Memory;

namespace InternalsViewer.Query.Tests;

public class EventPropertiesTests
{
    [Fact]
    public void GetProperties_Lists_Base_Properties_Before_The_Events_Own()
    {
        var latch = new LatchEvent
        {
            TimeUs = 1500,
            DurationUs = 250,
            PageAddress = new PageAddress(1, 42),
            LatchClass = LatchClass.BUF,
            LatchMode = LatchMode.KP,
            LatchAddress = 0x1234
        };

        var properties = latch.GetProperties();

        var names = properties.Select(p => p.Name).ToList();

        Assert.True(names.IndexOf("Time") < names.IndexOf("Page"));
        Assert.True(names.IndexOf("Page") < names.IndexOf("Latch Class"));
        Assert.Equal("1.500 ms", properties.Single(p => p.Name == "Time").Value);
        Assert.Equal("0x0000000000001234", properties.Single(p => p.Name == "Latch Address").Value);
        Assert.Equal("(1:42)", properties.Single(p => p.Name == "Page").Value);
    }

    [Fact]
    public void GetProperties_Formats_Enums_Booleans_And_Typed_Numbers()
    {
        var pool = new ObjectPoolEvent { IsHit = false, ObjectType = ColumnStoreObjectType.PrimaryDictionary, ColumnId = 3 };

        var memory = new MemoryEvent { GrantedMemoryKb = 2048 };

        var poolProperties = pool.GetProperties();

        var memoryProperties = memory.GetProperties();

        Assert.Equal("No", poolProperties.Single(p => p.Name == "Hit").Value);
        Assert.Equal("Primary Dictionary", poolProperties.Single(p => p.Name == "Object Type").Value);
        Assert.Equal("3", poolProperties.Single(p => p.Name == "Column").Value);
        Assert.Equal("2,048 KB", memoryProperties.Single(p => p.Name == "Granted Memory").Value);
        Assert.Equal(string.Empty, memoryProperties.Single(p => p.Name == "Used Memory").Value);
    }
}
