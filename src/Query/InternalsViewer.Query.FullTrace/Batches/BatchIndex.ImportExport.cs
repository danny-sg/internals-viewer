using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Batches.Enums;

namespace InternalsViewer.Query.FullTrace.Batches;

public sealed partial class BatchIndex
{
    internal void Write(BinaryWriter writer)
    {
        writer.Write(Captures.Length);

        foreach (var capture in Captures)
        {
            writer.Write(capture.Function);

            writer.Write(capture.Instance);

            writer.Write(capture.Call);

            writer.Write(capture.Id);

            writer.Write(capture.Iterator);

            writer.Write(capture.Address);

            writer.Write(capture.Consumer);

            writer.Write((byte)capture.Flags);

            writer.Write(capture.Capacity);

            ExportBinary.WriteArray(writer, capture.Selection);

            writer.Write(capture.Vectors.Count);

            foreach (var vector in capture.Vectors)
            {
                WriteVector(writer, vector);
            }
        }
    }

    internal static BatchIndex Read(BinaryReader reader)
    {
        var captures = new BatchCapture[reader.ReadInt32()];

        for (var index = 0; index < captures.Length; index++)
        {
            var function = reader.ReadUInt64();

            var instance = reader.ReadUInt64();

            var call = reader.ReadInt32();

            var id = reader.ReadUInt64();

            var iterator = reader.ReadUInt64();

            var address = reader.ReadUInt64();

            var consumer = reader.ReadUInt64();

            var flags = (BatchCaptureFlags)reader.ReadByte();

            var capacity = reader.ReadInt32();

            var selection = ExportBinary.ReadArray<ushort>(reader);

            var vectors = new BatchCaptureVector[reader.ReadInt32()];

            for (var ordinal = 0; ordinal < vectors.Length; ordinal++)
            {
                vectors[ordinal] = ReadVector(reader);
            }

            captures[index] = new BatchCapture(function,
                                               instance,
                                               call,
                                               id,
                                               iterator,
                                               address,
                                               consumer,
                                               flags,
                                               selection,
                                               vectors,
                                               capacity);
        }

        return new BatchIndex(captures);
    }

    private static void WriteVector(BinaryWriter writer, BatchCaptureVector vector)
    {
        writer.Write(vector.VectorId);

        writer.Write((byte)vector.Flags);

        ExportBinary.WriteArray(writer, vector.Slots);

        writer.Write(vector.DeepValues.Count);

        foreach (var (slot, deepValue) in vector.DeepValues)
        {
            writer.Write(slot);

            writer.Write(deepValue.Length);

            writer.Write(deepValue.Data.Length);

            writer.Write(deepValue.Data);
        }
    }

    private static BatchCaptureVector ReadVector(BinaryReader reader)
    {
        var vectorId = reader.ReadInt32();

        var flags = (BatchVectorFlags)reader.ReadByte();

        var slots = ExportBinary.ReadArray<ulong>(reader);

        var count = reader.ReadInt32();

        var deepValues = new Dictionary<int, BatchDeepValue>(count);

        for (var index = 0; index < count; index++)
        {
            var slot = reader.ReadInt32();

            var length = reader.ReadInt32();

            deepValues[slot] = new BatchDeepValue(length, reader.ReadBytes(reader.ReadInt32()));
        }

        return new BatchCaptureVector(vectorId, flags, slots, deepValues);
    }
}
