using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.vNext.Models;

namespace InternalsViewer.UI.App.vNext.ViewModels.Allocation;

internal class CalibrationBuilder
{
    public static List<AllocationLayer> GenerateLayers()
    {
        var layers = new List<AllocationLayer>();

        var layer1 = new AllocationLayer
        {
            Name = "Layer 1",
            IndexName = "Even numbers - 2 to 16 + 2, 4, 6",
            Allocations = new List<ExtentAllocation>
            {
                new(1, 2),
                new(1, 4),
                new(1, 6),
                new(1, 8),
                new(1, 10),
                new(1, 12),
                new(1, 14),
                new(1, 16),
            },
            SinglePages = new List<PageAddress> { new(1, 2), new(1, 4), new(1, 6), new(1, 8) },
            IsVisible = true,
            Colour = Color.Red

        };

        var layer2 = new AllocationLayer
        {
            Name = "Layer 2",
            IndexName = "Odd numbers - 17 to 21 + 1, 11, 13",
            Allocations = new List<ExtentAllocation>
            {
                new(1, 17),
                new(1, 19),
                new(1, 21),
            },
            SinglePages = new List<PageAddress> { new(1, 9), new(1, 11), new(1, 13) },
            IsVisible = true,
            Colour = Color.Blue
        };

        var layer3 = new AllocationLayer
        {
            Name = "Layer 3",
            IndexName = "Range - 40 - 65 + 2100, 2200, 2300, 2400",
            Allocations = Enumerable.Range(40, 25).Select(s => new ExtentAllocation(1, s)).ToList(),
            SinglePages = new List<PageAddress> { new(1, 2100), new(1, 2200), new(1, 2300), new(1, 2400) },
            IsVisible = true,
            Colour = Color.Green

        };

        layers.Add(layer1);
        layers.Add(layer2);
        layers.Add(layer3);

        return layers;
    }
}
