
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Address;

internal readonly record struct AddressMapFunction(ulong Address, AddressMapOperation Operation)
{
    public DeclaredClass[] DeclaredClasses { get; init; } = [];

    public int[] Pointers { get; init; } = [];
}
