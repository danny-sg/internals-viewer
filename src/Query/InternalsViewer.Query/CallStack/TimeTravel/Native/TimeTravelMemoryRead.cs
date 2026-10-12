using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;

namespace InternalsViewer.Query.CallStack.TimeTravel.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TimeTravelMemoryRead(ulong Function,
                                                     int Register,
                                                     uint AtReturn,
                                                     uint Offset,
                                                     uint Hops = 0,
                                                     uint FirstHop = 0,
                                                     uint SecondHop = 0,
                                                     uint ThirdHop = 0)
{
    public static TimeTravelMemoryRead From(LocatorRead read)
    {
        var path = read.Path ?? [];

        if (path.Length > LocatorReadResolver.MaximumHops)
        {
            throw new ArgumentException($"A read follows at most {LocatorReadResolver.MaximumHops} pointers", nameof(read));
        }

        uint HopAt(int index) => index < path.Length ? (uint)path[index] : 0;

        return new TimeTravelMemoryRead(read.Function,
                                        read.Register,
                                        read.AtReturn ? 1u : 0u,
                                        (uint)read.Offset,
                                        (uint)path.Length,
                                        HopAt(0),
                                        HopAt(1),
                                        HopAt(2));
    }
}
