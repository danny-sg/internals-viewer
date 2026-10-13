using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed class MemoryInUseCurve(double[] positions, ulong[] values)
{
    public static MemoryInUseCurve Empty { get; } = new([], []);

    public bool IsEmpty => Positions.Length == 0;

    private double[] Positions { get; } = positions;

    private ulong[] Values { get; } = values;

    public ulong ValueAt(double position)
    {
        var index = SortedSearch.FirstAfter(Positions, position) - 1;

        return index >= 0 ? Values[index] : 0;
    }

    public ulong PeakDuring(double start, double end)
    {
        var peak = ValueAt(start);

        for (var index = SortedSearch.FirstAfter(Positions, start); index < Positions.Length && Positions[index] < end; index++)
        {
            peak = Math.Max(peak, Values[index]);
        }

        return peak;
    }

    internal static MemoryInUseCurve Read(BinaryReader reader)
        => new(ExportBinary.ReadArray<double>(reader), ExportBinary.ReadArray<ulong>(reader));

    internal void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Positions);

        ExportBinary.WriteArray(writer, Values);
    }
}
