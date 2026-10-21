using System.Runtime.InteropServices;

namespace InternalsViewer.Query.FullTrace.Functions;

internal static class VtableResolver
{
    private const string VtableName = "::`vftable'";

    internal static string? GetClassName(ReadOnlySpan<char> name)
    {
        var end = name.IndexOf(VtableName, StringComparison.Ordinal);

        return end > 0 ? name[..end].ToString() : null;
    }

    internal sealed class Collector
    {
        private Dictionary<uint, string?> Classes { get; } = [];

        public void VisitData(uint rva, ReadOnlySpan<char> name)
        {
            if (GetClassName(name) is not { } className)
            {
                return;
            }

            ref var existing = ref CollectionsMarshal.GetValueRefOrAddDefault(Classes, rva, out var exists);

            existing = !exists || existing == className ? className : null;
        }

        public Dictionary<ulong, string> Build()
            => Classes.Where(c => c.Value is not null).ToDictionary(c => (ulong)c.Key, c => c.Value!);
    }
}
