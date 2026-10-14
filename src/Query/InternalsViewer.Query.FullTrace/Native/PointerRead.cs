using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PointerRead(ulong Function,
                                            int Register,
                                            uint IsReadAtReturn,
                                            uint Offset,
                                            uint Hops = 0,
                                            uint FirstHop = 0,
                                            uint SecondHop = 0,
                                            uint ThirdHop = 0,
                                            uint Mode = 0)
{
    public static PointerRead From(LocatorRead read)
    {
        var path = read.Path ?? [];

        if (path.Length > LocatorReadResolver.MaximumHops)
        {
            throw new ArgumentException($"A read follows at most {LocatorReadResolver.MaximumHops} pointers", nameof(read));
        }

        return new PointerRead(read.Function,
                               read.Register,
                               read.IsReadAtReturn ? 1u : 0u,
                               (uint)read.Offset,
                               (uint)path.Length,
                               HopAt(0),
                               HopAt(1),
                               HopAt(2),
                               ModeOf(read.Kind));

        uint HopAt(int index) => index < path.Length ? (uint)path[index] : 0;
    }

    public static uint ModeOf(LocatorKind kind) => kind switch
    {
        LocatorKind.Argument => 1,
        LocatorKind.Text => 2,
        LocatorKind.WideText => 3,
        LocatorKind.TypeInfo => 4,
        _ => 0
    };
}
