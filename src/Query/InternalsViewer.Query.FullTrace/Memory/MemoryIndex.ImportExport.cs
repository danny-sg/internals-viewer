using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryIndex
{
    internal static MemoryIndex Read(BinaryReader reader)
    {
        var count = reader.ReadInt32();

        var threads = new Dictionary<uint, ThreadMemory>(count);

        for (var index = 0; index < count; index++)
        {
            var thread = reader.ReadUInt32();

            threads[thread] = ThreadMemory.Read(reader);
        }

        var inUse = MemoryInUseCurve.Read(reader);

        return new MemoryIndex(threads, inUse, MemoryInUseCurve.Read(reader));
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Threads.Count);

        foreach (var (thread, memory) in Threads)
        {
            writer.Write(thread);

            memory.Write(writer);
        }

        InUse.Write(writer);

        Workspace.Write(writer);
    }

    private sealed partial class ThreadMemory
    {
        private ThreadMemory(TracePosition[] allocationStarts,
                             ulong[] allocationBytes,
                             TracePosition[] freedAt,
                             TracePosition[] freeStarts,
                             ulong[] freeTotals)
        {
            AllocationStarts = allocationStarts;
            AllocationBytes = allocationBytes;
            FreedAt = freedAt;
            AllocationTotals = GetRunningTotals(allocationBytes);
            FreeStarts = freeStarts;
            FreeTotals = freeTotals;
        }

        public static ThreadMemory Read(BinaryReader reader)
            => new(ExportBinary.ReadArray<TracePosition>(reader),
                   ExportBinary.ReadArray<ulong>(reader),
                   ExportBinary.ReadArray<TracePosition>(reader),
                   ExportBinary.ReadArray<TracePosition>(reader),
                   ExportBinary.ReadArray<ulong>(reader));

        public void Write(BinaryWriter writer)
        {
            ExportBinary.WriteArray(writer, AllocationStarts);

            ExportBinary.WriteArray(writer, AllocationBytes);

            ExportBinary.WriteArray(writer, FreedAt);

            ExportBinary.WriteArray(writer, FreeStarts);

            ExportBinary.WriteArray(writer, FreeTotals);
        }
    }
}
